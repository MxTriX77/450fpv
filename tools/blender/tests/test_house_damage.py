"""The `house` generator's D state (p1-h01-adobe-house task 1.4): the rubble's volume rule (the removed solid volume
x 1.86, within 5 %), the spread (pieces per metre band from the wall within 20 % of exp(-d / lambda)), the roof-bay
gap rule (a bay is a gap only where no batten stands), the jagged polygon's inscribed circle (a breach's clear space
after its blocks, against a brute-force search), and H01_D's params, gaps, pieces, colours, LODs and metadata."""
import collections
import copy
import math
import random
from pathlib import Path

import pytest

from generators import house
from lib import audit, catalog, joinery, meta, params as params_lib, rects, roof, rubble, walls
from lib.geometry import Mesh

ROOT = Path(__file__).resolve().parents[3]
MM5 = 0.005


@pytest.fixture(scope="module")
def d_params():
    entry = catalog.load(ROOT)["assets"]["H01"]
    return entry, catalog.load_params(catalog.params_path(ROOT, entry, "H01_D"))


@pytest.fixture(scope="module")
def d_built(d_params):
    entry, params = d_params
    out = house.build(params)
    return params, out, meta.assemble("H01", "H01_D", "house", params, out, entry["materials"])


def with_(params, **changes):
    p = copy.deepcopy(params)
    for key, value in changes.items():
        node = p
        parts = key.split(".")
        for part in parts[:-1]:
            node = node[part]
        node[parts[-1]] = value
    return p


def test_h01_d_params_are_design_11s(d_params):
    _, p = d_params
    assert params_lib.validate(house, p) == []
    assert (p["damage"], p["roof_strip"], p["batten_break"], p["rafters_broken"]) == (0.55, 0.75, 0.3, 2)
    assert (p["door_leaf_state"], p["opening_jag"], p["shard_heap_depth"]) == ("hanging", 0.2, 0.3)
    assert (p["interior_ceiling_down"], p["hanging_cables"], p["rubble_volume"]) == (0.45, 1, -1.0)
    assert all(w["state"] == "frame_empty" for w in p["windows"].values() if w["side"] != "none")
    b = p["wall_breaches"]["b1"]
    assert (b["side"], b["w"], b["h"]) == ("back", 0.9, 1.2)


@pytest.mark.parametrize("key,value,needle", [
    ("damage", 0.9, "the X state"),
    ("collapse.c1.wall", "back", "not built yet"),
    ("gable_breach", 0.4, "not built yet"),
    ("windows.w1.state", "frame_gone", "no frame"),
    ("rafters_broken", 1, "in pairs"),
    ("opening_jag", 0.4, "crumble"),  # 0.6 m of crumble round w3 reaches the corner's pier
])
def test_check_refuses_what_d_does_not_build(d_params, key, value, needle):
    _, p = d_params
    problems = params_lib.validate(house, with_(p, **{key: value}))
    assert any(needle in q for q in problems), problems


# --- the rubble ----------------------------------------------------------------------------------------------------

def test_rubble_volume_is_the_removed_volume_times_the_bulking(d_built):
    _, out, _ = d_built
    rb = out["rubble"]
    removed = sum(rb.removed.values())
    laid = sum(rb.core.values()) + rubble.BULKING * sum(rb.pieces.values())
    assert removed > 1.0
    assert abs(laid / (rubble.BULKING * removed) - 1) <= 0.05, (laid, removed)
    for cls in rb.removed:  # and class by class: each heap is made of what came down there
        rule = rubble.BULKING * rb.removed[cls]
        got = rb.core.get(cls, 0.0) + rubble.BULKING * rb.pieces.get(cls, 0.0)
        assert abs(got / rule - 1) <= 0.05, (cls, got, rule)


def test_rubble_volume_param_scales_the_heaps(d_params):
    _, p = d_params
    rb = house.build(with_(p, rubble_volume=20.0))["rubble"]
    laid = sum(rb.core.values()) + rubble.BULKING * sum(rb.pieces.values())
    assert abs(laid - 20.0) <= 0.05 * 20.0, laid


def _bands_hold(distances, lam, tolerance=0.2, least=5.0):
    """Each metre band [k, k + 1) holds its exp(-d / lam) share of the pieces within `tolerance`, for every band
    whose share is at least `least` pieces."""
    n = len(distances)
    k, checked = 0, 0
    while True:
        expected = n * (math.exp(-k / lam) - math.exp(-(k + 1) / lam))
        if expected < least:
            return checked
        got = sum(k <= d < k + 1 for d in distances)
        assert abs(got - expected) <= tolerance * expected, (k, got, expected, lam)
        k, checked = k + 1, checked + 1


@pytest.mark.parametrize("lam", [1.0, 1.5, 2.0, 3.5])
def test_spread_is_stratified_on_exp(lam):
    d = rubble.spread(random.Random(3), 300, lam)
    assert _bands_hold(d, lam) >= 2


def test_house_rubble_spreads_by_exp_per_class_and_widens_away_from_the_burst(d_built):
    params, out, _ = d_built
    groups = collections.defaultdict(list)
    lams = {}
    for foot, cls, d, lam, call in out["rubble"].spread_log:  # each scatter is stratified on its own
        groups[call].append(d)
        lams[call] = lams[foot, cls] = lam
    checked = 0
    for call, ds in groups.items():
        if len(ds) >= 40:
            checked += _bands_hold(ds, lams[call])
    assert checked >= 6
    assert lams["right outer", "tile"] == pytest.approx(rubble.LAMBDA_M["tile"] * rubble.LEE_WIDEN)  # the lee end
    assert lams["front outer", "tile"] == pytest.approx(rubble.LAMBDA_M["tile"])


def test_rubble_pieces_carry_their_own_collision_and_the_houses_colours(d_built):
    _, out, data = d_built
    rb = out["rubble"]
    assert rb.counts["tile"] >= 100 and rb.counts["timber"] >= 20 and rb.counts["adobe"] >= 20
    assert len(rb.collision) >= sum(rb.counts.values())  # a shape (or more) per piece, and the heaps' boxes
    slots = set().union(*(m.slots() for m in rb.lods))
    assert slots <= {"tile", "tile_fracture", "timber", "timber_fracture", "iron", "adobe", "clay", "heap"}
    assert not slots & {"facade", "interior", "glass"}  # never whitewash: rubble is the house's own materials
    recipes = {m["slot"]: m["visual"] for m in data["materials"]}
    assert {recipes[s] for s in slots} <= {"clay_tile", "tile_fracture", "roof_timber", "timber_fracture", "car_rust",
                                           "adobe", "clay_render"}  # the heap is the tiles' clay (the sheet: as tiles)


# --- the roof's gaps -----------------------------------------------------------------------------------------------

def test_a_roof_bay_is_a_gap_only_where_no_batten_stands(d_params, d_built):
    _, p = d_params
    params, out, data = d_built
    plan = walls.Plan(params)
    layout = roof.Layout(plan, params, roof.Slope(plan, "front").S)
    bw = roof.BATTEN_M[0]
    bays = [g for g in data["gaps"] if g["id"].startswith("roof_")]
    assert bays
    for g in bays:  # each opens between two battens left standing: at least two gauges less a batten up the slope
        ys = [q[1] for q in g["polygon"]]
        rise = (max(ys) - min(ys)) / plan.sin
        assert rise >= 2 * layout.gauge - bw - 0.03, (g["id"], rise)
    # with every batten standing, a stripped roof with rafters off has no gap at all
    still = house.build(with_(p, batten_break=0.0, interior_ceiling_down=0.0, opening_jag=0.0,
                              **{"wall_breaches.b1.side": "none"}))
    assert not [g for g in still["gaps"] if g["id"].startswith("roof_")]


def test_roof_bays_open_between_rafter_faces_less_their_stubs(d_built):
    params, out, data = d_built
    plan = walls.Plan(params)
    for g in data["gaps"]:
        if not g["id"].startswith("roof_"):
            continue
        xs = [q[0] for q in g["polygon"]]
        assert max(xs) - min(xs) <= 2 * params["rafter_pitch"] - plan.rafter_w + 1e-6  # at most a rafter gone
        assert g["narrowest_m"] <= max(xs) - min(xs) + 1e-6
        assert abs(g["narrowest_m"] - audit.inscribed_diameter(g["polygon"], g["normal"])) <= 1e-4


# --- the jagged polygon's inscribed circle -------------------------------------------------------------------------

def _brute_inscribed(loop, step=0.005):
    """The largest circle inside a simple polygon by brute force: the farthest point from its edges on a grid."""
    xs, ys = [p[0] for p in loop], [p[1] for p in loop]
    best = 0.0
    edges = list(zip(loop, loop[1:] + loop[:1]))
    y = min(ys) + step / 2
    while y < max(ys):
        x = min(xs) + step / 2
        while x < max(xs):
            if audit._inside_polygon2((x, y), loop):
                best = max(best, min(audit._segment_distance((x, y), a, b) for a, b in edges))
            x += step
        y += step
    return 2 * best


def test_jagged_polygon_inscribed_circle():
    blocks = [(0.0, 0.15, 0.0, 0.2), (0.75, 0.9, 0.2, 0.4), (0.0, 0.1, 0.4, 0.6), (0.8, 0.9, 0.6, 0.8),
              (0.0, 0.2, 0.8, 1.0), (0.85, 0.9, 1.0, 1.2), (0.25, 0.5, 1.1, 1.2), (0.5, 0.75, 0.0, 0.07)]
    cells = rects.free((0.0, 0.9, 0.0, 1.2), blocks)
    loop = rects.outline(cells)
    assert abs(rects.area(cells) - (0.9 * 1.2 - sum((b[1] - b[0]) * (b[3] - b[2]) for b in blocks))) < 1e-9
    poly3 = [[x, y, 0.0] for x, y in loop]
    d = audit.inscribed_diameter(poly3, [0.0, 0.0, 1.0])
    assert abs(d - _brute_inscribed(loop)) <= MM5, (d, _brute_inscribed(loop))
    assert d < 0.9 - 0.15  # the jags take their share of the width
    straight = rects.outline(rects.free((0.0, 0.9, 0.0, 3.0), [(0.0, 0.2, 0.0, 3.0)]))
    assert abs(audit.inscribed_diameter([[x, y, 0.0] for x, y in straight], [0.0, 0.0, 1.0]) - 0.7) <= 0.002


def test_breach_gap_is_its_clear_space_after_the_blocks(d_params, d_built):
    _, p = d_params
    params, out, data = d_built
    plan = walls.Plan(params)
    walls.damage(plan, params, lambda what: random.Random(f"house {params['seed']} {what}"))
    (o,) = [o for o in plan.openings if o.kind == "breach"]
    assert o.blocks and abs((o.a1 - o.a0) - (0.9 + 0.2)) <= 1e-9 and abs((o.y1 - o.y0) - (1.2 + 0.2)) <= 1e-9
    gap = next(g for g in data["gaps"] if g["id"] == o.name)
    side = plan.sides[o.side]
    loop = rects.outline(o.clear)
    flat = [(a, y) for a, y in loop]
    assert abs(gap["narrowest_m"] - _brute_inscribed(flat)) <= MM5
    assert abs(rects.area(o.clear) - ((o.a1 - o.a0) * (o.y1 - o.y0) -
                                      rects.area(rects.union(o.blocks)))) < 1e-9
    assert gap["narrowest_m"] >= house.DRONE_M[0]  # the sheet: a 0.6-1.2 m breach passes nose-first after its jags
    assert all(abs(c - side.point(0, 0, plan.T / 2)[2]) < 1e-9 for c in [q[2] for q in gap["polygon"]])


# --- the D house ---------------------------------------------------------------------------------------------------

def test_d_metadata_validates_and_every_gap_matches_its_circle(d_built, material_ids):
    _, out, data = d_built
    assert meta.validate(data, material_ids) == []
    for g in data["gaps"]:
        assert abs(g["narrowest_m"] - audit.inscribed_diameter(g["polygon"], g["normal"])) <= 1e-4  # metadata: µm
    ids = {g["id"] for g in data["gaps"]}
    assert {"door", "breach_b1", "window_w1", "window_w4"} <= ids
    assert any(i.startswith("ceiling_") for i in ids) and any(i.startswith("roof_") for i in ids)
    (cavity,) = [w for w in data["wind_volumes"] if w.get("cavity")]
    assert "breach_b1" in cavity["openings"] and "window_w4" in cavity["openings"]


def test_d_windows_keep_their_frames_clear_and_crumble_round_them(d_built):
    params, out, data = d_built
    for g in data["gaps"]:
        if g["id"].startswith("window_"):
            w, h = joinery.extent(g)
            assert abs(w - 0.66) <= MM5 and abs(h - 0.86) <= MM5 and abs(g["narrowest_m"] - 0.66) <= MM5
    plan = walls.Plan(params)
    walls.damage(plan, params, lambda what: random.Random(f"house {params['seed']} {what}"))
    for o in plan.openings:
        if o.kind != "window":
            continue
        for face in ("outer", "inner"):
            cells = o.crumble[face]
            grown = rects.area(cells) - (o.a1 - o.a0) * (o.y1 - o.y0)
            perimeter = 2 * ((o.a1 - o.a0) + (o.y1 - o.y0))
            mean = grown / perimeter
            assert 0.1 <= mean <= 0.3, (o.name, face, mean)  # the sheet: crumbled edges 0.1-0.3 m


def test_d_lods_halve_and_stay_in_budget(d_built):
    _, out, data = d_built
    tris = [l["tris"] for l in data["lods"]]
    assert tris[0] < 150000, tris
    assert all(b <= a / 2 for a, b in zip(tris, tris[1:])), tris


def test_d_cable_hangs_in_the_hall(d_built):
    params, out, data = d_built
    plan = walls.Plan(params)
    hall = next(s for s in plan.spaces if s.name == "hall")
    cables = [c for c in data["collision"] if c["material"] == "cable"]
    assert len(cables) == house.CABLE_SEGMENTS
    assert all(hall.x0 < c["center"][0] < hall.x1 and hall.z0 < c["center"][2] < hall.z1 for c in cables)
    lowest = min(c["center"][1] for c in cables)
    assert plan.H - roof.JOIST_M[1] - house.CABLE_SAG_M[1] - 0.01 <= lowest <= plan.H - 0.4
    assert any(s["kind"] == "hook" and abs(s["radius_m"] - house.CABLE_R_M) < 1e-9 for s in data["snag"])


def test_d_ceiling_down_share_and_its_holes(d_built):
    params, out, data = d_built
    plan = walls.Plan(params)
    lines = [l for l in out["lines"] if l.startswith("damage:")]
    (line,) = lines
    down, total = (float(v) for v in line.split("ceiling down ")[1].split(" m2")[0].split(" of "))
    assert params["interior_ceiling_down"] - 0.02 <= down / total <= params["interior_ceiling_down"] + 0.06
    hall = next(s for s in plan.spaces if s.name == "hall")
    for g in data["gaps"]:
        if g["id"].startswith("ceiling_"):
            xs = [q[0] for q in g["polygon"]]
            assert not (hall.x0 < (min(xs) + max(xs)) / 2 < hall.x1)  # the ceiling stays over the door room
