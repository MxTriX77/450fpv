"""generators.house and lib.walls, lib.joinery, lib.roof and the mask atlas of lib.masks (p1-h01-adobe-house task 1.3,
the intact house): the exact AST-04 sizes (door, window, brick, batten, rafter pitch, walls, ridge, within 5 mm), the
facade's U continuing across every corner within 1 mm and folding into the reveals, the gap polygons of the door and
of a window in each of its states, the tiles' layout (gauge + lap, the ГОСТ size), the mask atlas following
render_loss, the metadata (materials, textures with channels, the cavity), the LODs halving, and the refusals of what
is not built yet."""
import copy
import json
import math
import random
from pathlib import Path

import pytest

from generators import house
from lib import catalog, joinery, masks, meta, params as params_lib, roof, walls
from lib.geometry import Mesh

ROOT = Path(__file__).resolve().parents[3]
MM5 = 0.005


@pytest.fixture(scope="module")
def h01():
    entry = catalog.load(ROOT)["assets"]["H01"]
    assert entry["generator"] == "house" and entry["class"] == "houses"
    return entry, catalog.load_params(catalog.params_path(ROOT, entry, "H01_I"))


@pytest.fixture(scope="module")
def built(h01):
    entry, params = h01
    out = house.build(params)
    data = meta.assemble("H01", "H01_I", "house", params, out, entry["materials"])
    return params, out, data


def with_(params, **changes):
    p = copy.deepcopy(params)
    for key, value in changes.items():
        node = p
        parts = key.split(".")
        for part in parts[:-1]:
            node = node[part]
        node[parts[-1]] = value
    return p


# --- params and refusals --------------------------------------------------------------------------------------------

def test_h01_i_params_validate(h01):
    entry, params = h01
    assert entry["variants"] == ["H01_I"]
    assert params_lib.validate(house, params) == []
    assert params["damage"] == 0 and params["wall_material"] == "adobe" and params["roof_cover"] == "clay_tile"
    assert (params["roof_type_front"], params["roof_type_back"]) == ("gable", "hip")


@pytest.mark.parametrize("key,value,needle", [
    ("damage", 0.55, "not built yet"),
    ("roof_strip", 0.75, "not built yet"),
    ("batten_break", 0.3, "not built yet"),
    ("rafters_broken", 2, "not built yet"),
    ("opening_jag", 0.2, "not built yet"),
    ("interior_ceiling_down", 0.45, "not built yet"),
    ("hanging_cables", 1, "not built yet"),
    ("shard_heap_depth", 0.3, "not built yet"),
    ("rubble_volume", 30.0, "not built yet"),
    ("wall_breaches.b1.side", "back", "not built yet"),
    ("collapse.c1.wall", "left", "not built yet"),
    ("door_leaf_state", "inside", "not built yet"),
    ("wall_material", "silicate", "not built yet"),
    ("roof_cover", "slate", "not built yet"),
    ("roof_type_back", "half_hip", "not built yet"),
    ("veranda", True, "not built yet"),
    ("door_m.width", 0.9, "AST-04"),
    ("windows.w1.h", 1.2, "AST-04"),
    ("batten_pitch", 0.65, "passes the drone"),
    ("windows.w2.pos", 0.62, "partition"),
    ("windows.w1.pos", 0.05, "corner"),
])
def test_check_refuses(h01, key, value, needle):
    _, params = h01
    problems = params_lib.validate(house, with_(params, **{key: value}))
    assert any(needle in p for p in problems), problems


def test_check_refuses_a_head_lap_outside_the_tiles_range(h01):
    _, params = h01
    problems = params_lib.validate(house, with_(params, **{"tile_m.length": 0.36}))  # 27 mm on 0.333 m battens
    assert any("head lap" in p for p in problems), problems


# --- the exact sizes (AST-04 and the sheet, within 5 mm) ---------------------------------------------------------

def test_walls_and_ridge(h01):
    _, params = h01
    plan = walls.Plan(params)
    assert abs(plan.H - 2.40) <= MM5 and abs(plan.T - 0.45) <= MM5
    assert abs(plan.apex - 5.0) <= MM5, plan.apex  # the rafters' apex at the sheet's 5.0 m ridge


def test_door_window_and_sill(h01):
    _, params = h01
    plan = walls.Plan(params)
    door = plan.door
    jamb, head = joinery.DOOR_FRAME_M
    assert abs((door.a1 - door.a0 - 2 * jamb) - 0.80) <= MM5
    assert abs((door.y1 - head - plan.F) - 1.90) <= MM5
    windows = [o for o in plan.openings if o.kind == "window"]
    assert len(windows) == 6
    for o in windows:
        assert abs(o.a1 - o.a0 - 0.80) <= MM5 and abs(o.y1 - o.y0 - 1.00) <= MM5 and abs(o.y0 - 0.9) <= MM5
        f = joinery.WINDOW_FRAME_M
        assert abs((o.a1 - o.a0 - 2 * f) - 0.66) <= MM5 and abs((o.y1 - o.y0 - 2 * f) - 0.86) <= MM5


def test_brick_is_ast04():
    assert roof.BRICK_M == (0.25, 0.065, 0.12)
    from generators import debris_brick
    lo, hi = debris_brick.brick_mesh(roof.BRICK_M).bounds()
    assert all(abs((hi[k] - lo[k]) - s) <= 1e-9 for k, s in enumerate(roof.BRICK_M))
    # a course's six bricks fill the 0.51 m square round the flue without overlapping
    rects = []
    for cx, cz, along_x in roof._course_bricks(0.0, 0.0, 0) + roof._course_bricks(0.0, 0.0, 1):
        hx, hz = (0.125, 0.06) if along_x else (0.06, 0.125)
        rects.append((cx - hx, cx + hx, cz - hz, cz + hz))
    for k in (0, 6):
        course = rects[k:k + 6]
        assert all(-1e-9 <= r[0] and r[1] <= 0.51 + 1e-9 and -1e-9 <= r[2] and r[3] <= 0.51 + 1e-9 for r in course)
        for i, a in enumerate(course):
            for b in course[i + 1:]:
                assert a[1] <= b[0] + 1e-9 or b[1] <= a[0] + 1e-9 or a[3] <= b[2] + 1e-9 or b[3] <= a[2] + 1e-9


def test_battens_and_rafter_pitch(built):
    params, out, data = built
    plan = walls.Plan(params)
    slope = roof.Slope(plan, "front")
    layout = roof.Layout(plan, params, slope.S)
    gauges = [b - a for a, b in zip(layout.battens, layout.battens[1:])]
    assert all(abs(g - 0.33) <= MM5 for g in gauges), gauges
    assert roof.BATTEN_M == (0.05, 0.05)
    # every batten's box is 50 x 50 mm in its own frame (the front slope's battens run along X)
    battens = [c for c in data["collision"] if c["material"] == "timber"
               and sorted(round(s, 4) for s in c["size"])[:2] == [0.05, 0.05]]
    assert len(battens) >= 3 * len(layout.battens)
    commons, forced, _ = roof.rafter_layout(plan, params["rafter_pitch"])
    bays = [b - a for a, b in zip(commons, commons[1:])]
    assert all(abs(b - 1.0) <= MM5 for b in bays[1:]), bays  # the first bay is the gable's short one
    assert abs(bays[0] - 0.45) <= MM5


def test_tiles_are_the_gost_strip_tile(built):
    params, out, _ = built
    plan = walls.Plan(params)
    layout = roof.Layout(plan, params, roof.Slope(plan, "front").S)
    assert (params["tile_m"]["length"], params["tile_m"]["width"], params["tile_m"]["cover_width"]) == (0.40, 0.22, 0.20)
    assert abs(layout.L - (layout.gauge + layout.lap)) < 1e-12 and abs(layout.lap - 0.067) <= 1e-9
    assert 0.04 <= layout.e <= house.TAIL_MAX_M
    assert any(line.startswith("tiles:") and "head lap 67 mm" in line for line in out["lines"])


# --- UVs: the facade loop and the folded reveals -----------------------------------------------------------------

def _facade_uvs_at(mesh, point, tol=1e-7):
    """Every facade-slot face corner at `point`: (face normal, uv)."""
    found = []
    for indices, normal, uvs, slot in mesh.faces:
        if slot != walls.FACADE_SLOT:
            continue
        for i, uv in zip(indices, uvs):
            if math.dist(mesh.vertices[i], point) < tol:
                found.append((normal, uv))
    return found


def test_facade_u_continues_across_every_corner(built):
    params, out, _ = built
    plan = walls.Plan(params)
    mesh = out["lods"][0]
    perimeter = plan.perimeter
    for x, z in ((plan.X, plan.Z), (-plan.X, plan.Z), (-plan.X, -plan.Z), (plan.X, -plan.Z)):
        top = plan.rt(x, z) if plan.ends[1 if x > 0 else -1] == "gable" else plan.H
        for y in (0.0, 0.9, top):
            found = [(n, uv) for n, uv in _facade_uvs_at(mesh, (x, y, z)) if abs(n[1]) < 0.5]
            sides = {(round(n[0]), round(n[2])) for n, _ in found}
            assert len(sides) == 2, (x, y, z, found)
            us = sorted(uv[0] for _, uv in found)
            gap = us[-1] - us[0]
            # the loop restarts once, at the north-east corner, where it has run the whole perimeter
            expected = perimeter if (x > 0 and z > 0) else 0.0
            assert abs(gap - expected) <= 0.001, (x, y, z, us)
            assert all(abs(uv[1] - y) <= 1e-9 for _, uv in found)  # V is up, in metres


def test_reveals_fold_the_facade_uv(built):
    params, out, _ = built
    plan = walls.Plan(params)
    mesh = out["lods"][0]
    o = next(o for o in plan.openings if o.name == "window_w1")
    side = plan.sides[o.side]
    for y in (o.y0, o.y1):  # the jamb's arris corners, where the outer face's grid has its vertices
        found = _facade_uvs_at(mesh, side.point(o.a0, y, 0.0))
        face = [uv for n, uv in found if abs(n[2] - side.outward()[2]) < 1e-9 and abs(n[0] - side.outward()[0]) < 1e-9]
        jamb = [uv for n, uv in found if abs(prism_dot(n, side.across()) - 1) < 1e-9]
        assert face and jamb and abs(face[0][0] - jamb[0][0]) < 1e-9 and abs(face[0][1] - jamb[0][1]) < 1e-9
        deep = _facade_uvs_at(mesh, side.point(o.a0, y, o.zones[0]))
        assert any(abs(uv[0] - (side.u0 + o.a0 + o.zones[0])) < 1e-9 for _, uv in deep)  # U folds on by the depth


def prism_dot(a, b):
    return sum(x * y for x, y in zip(a, b))


# --- gaps -----------------------------------------------------------------------------------------------------------

def _window_gaps(params, state):
    plan = walls.Plan(with_(params, **{"windows.w1.state": state}))
    o = next(o for o in plan.openings if o.name == "window_w1")
    res = joinery.window(plan, o, [Mesh(), Mesh(), Mesh(), Mesh()], random.Random(1))
    return plan, o, res.gaps


def test_door_gap_is_the_clear_opening(built):
    params, out, data = built
    gap = next(g for g in data["gaps"] if g["id"] == "door")
    w, h = joinery.extent(gap)
    assert abs(w - 0.80) <= MM5 and abs(h - 1.90) <= MM5 and abs(gap["narrowest_m"] - 0.80) <= MM5
    assert gap["normal"] == [0.0, 0.0, -1.0]
    assert "any yaw" in house.verdict(gap["narrowest_m"], h)
    plan = walls.Plan(params)
    closed = joinery.door(plan, walls.Plan(with_(params, door_leaf_state="closed")).door, [Mesh()] * 4,
                          random.Random(1))
    assert closed.gaps == []  # a closed leaf seals it


def test_framed_window_gap_and_the_other_states(h01):
    _, params = h01
    _, _, gaps = _window_gaps(params, "frame_empty")
    (gap,) = gaps
    w, h = joinery.extent(gap)
    assert abs(w - 0.66) <= MM5 and abs(h - 0.86) <= MM5 and abs(gap["narrowest_m"] - 0.66) <= MM5
    assert "+-18 deg" in house.verdict(gap["narrowest_m"], h)  # the sheet: passable within 18 degrees, 43 mm a side
    _, _, (gone,) = _window_gaps(params, "frame_gone")
    assert [round(v, 3) for v in joinery.extent(gone)] == [0.8, 1.0]
    _, _, (hanging,) = _window_gaps(params, "sash_hanging")
    assert hanging["narrowest_m"] < house.DRONE_M[0]  # a sash remnant: not passable until broken
    _, _, glazed = _window_gaps(params, "glazed")
    assert glazed == []
    _, _, panes = _window_gaps(params, "shards")
    assert len(panes) == 6 and all(0.2 < g["narrowest_m"] <= 0.24 + 1e-9 for g in panes)


def test_every_gap_matches_its_inscribed_circle(built):
    from lib import audit
    _, _, data = built
    for g in data["gaps"]:
        assert abs(g["narrowest_m"] - audit.inscribed_diameter(g["polygon"], g["normal"])) <= 1e-6


# --- metadata, textures, LODs -------------------------------------------------------------------------------------

def test_metadata_validates_with_the_mask_and_the_cavity(built, material_ids):
    _, out, data = built
    assert meta.validate(data, material_ids) == []
    assert {m["slot"]: m["physics"] for m in data["materials"]}["tile"] == "masonry"
    textures = {t["slot"]: t for t in data["textures"]}
    assert set(textures) == {"facade", "interior"}
    for t in textures.values():
        assert t["role"] == "damage_mask" and t["file"] == "H01_I_damage.png"
        assert t["channels"] == ["render_depth", "soot", "dirt", "base_band"]
        assert t["uv_min"] == [0.0, 0.0] and t["uv_max"] == list(walls.ATLAS_M)
    (cavity,) = [w for w in data["wind_volumes"] if w.get("cavity")]
    assert cavity["porosity"] == 1.0 and "door" in cavity["openings"]
    assert any(o.startswith("window_w4") for o in cavity["openings"])  # the far window across the hall


def test_lods_halve_and_keep_the_openings(built):
    params, out, data = built
    tris = [l["tris"] for l in data["lods"]]
    assert all(b <= a / 2 for a, b in zip(tris, tris[1:])), tris
    assert [l["switch_distance_m"] for l in data["lods"]] == [0.0, 25.0, 100.0, 400.0]
    assert data["cull_distance_m"] == pytest.approx(935 * 5.0 / 2, rel=0.01)
    plan = walls.Plan(params)
    door = plan.door
    side = plan.sides[door.side]
    centre = side.point((door.a0 + door.a1) / 2, 1.0, 0.0)
    for mesh in out["lods"]:  # no LOD closes the door's opening: no face of the front wall's plane covers it
        for indices, normal, _, slot in mesh.faces:
            if slot in (walls.FACADE_SLOT,) and abs(normal[2] + 1) < 1e-9:
                xs = [mesh.vertices[i][0] for i in indices]
                ys = [mesh.vertices[i][1] for i in indices]
                assert not (min(xs) < centre[0] < max(xs) and min(ys) < centre[1] < max(ys))


def test_mask_follows_render_loss(h01):
    _, params = h01
    plan = walls.Plan(params)
    regions = walls.build(plan, Mesh())[:1]  # the facade loop only, for speed

    def rngs(what):
        return random.Random(f"house 1 {what}")

    full = []
    for loss in (0.3, 0.6):
        raster, lines = masks.facade_atlas(regions, loss, 0.3, 0.42, 0, rngs, walls.ATLAS_M)
        r = raster.planes[0]
        full.append(sum(1 for v in r if v >= 192))
        share = float(lines[1].split("bare adobe on ")[1].split()[0])
        assert abs(share - loss) <= 0.05, lines[1]
    assert full[1] > full[0]
    again, _ = masks.facade_atlas(regions, 0.3, 0.3, 0.42, 0, rngs, walls.ATLAS_M)
    first, _ = masks.facade_atlas(regions, 0.3, 0.3, 0.42, 0, rngs, walls.ATLAS_M)
    assert again.png() == first.png()  # deterministic


def test_mask_covers_peak_as_the_layers_read_them(h01):
    """G soot peaks at 0.85 and B dirt at 0.4 (the layers cover by the channel's value, task 1.2), A is the band's
    cover, worn below 1 in places and nothing above the band."""
    _, params = h01
    plan = walls.Plan(params)
    regions = walls.build(plan, Mesh())[:1]
    raster, _ = masks.facade_atlas(regions, 0.3, 0.3, 0.42, 2, lambda w: random.Random(f"house 1 {w}"), walls.ATLAS_M)
    r, g, b, a = raster.planes
    assert 0.7 * 255 <= max(g) <= round(0.85 * 255)  # its streaks take up to a quarter off the plume's peak
    assert 0.35 * 255 <= max(b) <= round(0.4 * 255)
    w, h = masks.ATLAS_PX
    band = [a[y * w + x] for y in range(h - int(0.38 * 64), h) for x in range(0, w, 7)]
    above = [a[y * w + x] for y in range(h - int(1.0 * 64), h - int(0.5 * 64)) for x in range(0, w, 7)]
    assert max(band) == 255 and min(v for v in band if v) < 230 and max(above) == 0


def test_mask_meets_itself_where_the_facade_loop_restarts(h01):
    _, params = h01
    plan = walls.Plan(params)
    regions = walls.build(plan, Mesh())[:1]
    raster, _ = masks.facade_atlas(regions, 0.3, 0.3, 0.42, 0, lambda w: random.Random(f"house 1 {w}"), walls.ATLAS_M)
    w, h = masks.ATLAS_PX
    rows = range(h - int(2.3 * 64), h)  # the wall below the head, where the wall stands all round
    r = raster.planes[0]
    diffs = [abs(r[y * w] - r[y * w + w - 1]) for y in rows]
    assert sum(diffs) / len(diffs) < 12  # the first and last columns, 1/64 m either side of the corner, agree
