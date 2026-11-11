"""lib.slabs (w4b-farmland-ruins): collision boxes from a mesh's faces, on small meshes whose answer is known."""
import math

import pytest

from lib import air, slabs

SETTINGS = {"raster_m": 0.01, "angle_tol_deg": 6.0, "axis_angle_deg": 6.0, "axis_dev_m": 0.015,
            "plane_tol_m": 0.03, "max_thickness_m": 0.35, "merge_tol_m": 0.015, "min_box_m": 0.02,
            "cover_tol_m": 0.015}


def quad(a, b, c, d, n, part):
    return [((a, b, c), n, part), ((a, c, d), n, part)]


def wall_face(x, n, part):
    """A 4 x 3 m face at x (z 0..4, y 0..3) with a 1 x 1 m window (z 1.5..2.5, y 1..2) cut out."""
    out = []
    for z0, z1, y0, y1 in ((0, 1.5, 0, 3), (2.5, 4, 0, 3), (1.5, 2.5, 0, 1), (1.5, 2.5, 2, 3)):
        out += quad((x, y0, z0), (x, y0, z1), (x, y1, z1), (x, y1, z0), n, part)
    return out


def covers(boxes, p):
    for b in boxes:
        f = slabs._frame(b)
        if slabs._inside(p, f, 1e-9):
            return True
    return False


def test_two_faces_enclose_a_layered_wall_with_its_window_clear():
    parts = [slabs.Part("wall", "timber"), slabs.Part("wall", "masonry", layer_m=0.02)]
    out = slabs.decompose(wall_face(0.2, (1, 0, 0), 0) + wall_face(0.0, (-1, 0, 0), 1), parts, SETTINGS)
    boxes = out["boxes"]
    lining = [b for b in boxes if parts[b["part"]].physics == "masonry"]
    core = [b for b in boxes if parts[b["part"]].physics == "timber"]
    assert lining and core
    assert all(b["size"][0] == pytest.approx(0.02) and b["center"][0] == pytest.approx(0.01) for b in lining)
    assert all(b["size"][0] == pytest.approx(0.18) for b in core)
    assert not covers(boxes, (0.1, 1.5, 2.0)), "the window stays clear"
    for p in ((0.1, 0.5, 0.5), (0.1, 2.5, 3.5), (0.1, 1.5, 0.7), (0.01, 2.9, 0.1)):
        assert covers(boxes, p)
    volume = sum(b["size"][0] * b["size"][1] * b["size"][2] for b in boxes)
    assert volume == pytest.approx(0.2 * (12 - 1), rel=1e-6)
    assert all(b["rotation"] == (0.0, 0.0, 0.0, 1.0) for b in boxes)


def test_a_single_face_is_a_sheet_behind_it():
    parts = [slabs.Part("floor", "timber", sheet_m=0.02, landable=True)]
    tris = quad((0, 0.4, 0), (2, 0.4, 0), (2, 0.4, 1), (0, 0.4, 1), (0, 1, 0), 0)
    (box,) = slabs.decompose(tris, parts, SETTINGS)["boxes"]
    assert box["center"] == pytest.approx((1.0, 0.39, 0.5))
    assert box["size"] == pytest.approx((2.0, 0.02, 1.0))


def test_a_sloped_sheet_becomes_a_rotated_box_on_its_plane():
    parts = [slabs.Part("roof", "asbestos_cement", sheet_m=0.02)]
    pitch = math.radians(35)
    n = (0.0, math.cos(pitch), -math.sin(pitch))
    up = (0.0, math.sin(pitch), math.cos(pitch))  # up the slope

    def corner(a, b):
        return (float(a), 3 + b * up[1], b * up[2])

    tris = quad(corner(0, 0), corner(4, 0), corner(4, 2), corner(0, 2), n, 0)
    (box,) = slabs.decompose(tris, parts, SETTINGS)["boxes"]
    assert math.sqrt(sum(c * c for c in box["rotation"])) == pytest.approx(1.0)
    assert sorted(box["size"]) == pytest.approx([0.02, 2.0, 4.0], abs=0.011)
    mid = corner(2, 1)
    # the box lies under the sheet: its centre sheet_m / 2 below the plane
    assert sum((box["center"][k] - mid[k]) * n[k] for k in range(3)) == pytest.approx(-0.01, abs=1e-6)


def test_a_sliver_thinner_than_a_pixel_still_gets_collision():
    parts = [slabs.Part("sheet", "timber", sheet_m=0.01)]
    tris = [(((0.0, 0.0, 0.0), (1.0, 0.0, 0.0), (1.0, 0.0, 0.004)), (0, 1, 0), 0)]  # 1 m long, 4 mm wide at most
    boxes = slabs.decompose(tris, parts, SETTINGS)["boxes"]
    assert covers(boxes, (0.9, -0.005, 0.002)), "the conservative raster keeps a sub-pixel spike"


def test_quaternion_from_basis_turns_the_axes_onto_the_columns():
    x, y = (0.0, 0.0, -1.0), (0.0, 1.0, 0.0)
    z = (x[1] * y[2] - x[2] * y[1], x[2] * y[0] - x[0] * y[2], x[0] * y[1] - x[1] * y[0])  # x cross y
    q = slabs.quaternion_from_basis(x, y, z)
    assert slabs._qrot(q, (1, 0, 0)) == pytest.approx(x)
    assert slabs._qrot(q, (0, 1, 0)) == pytest.approx(y)


def box(center, size, material):
    return {"center": center, "size": size, "rotation": (0, 0, 0, 1), "m": material}


def test_prune_drops_a_sliver_of_its_own_material_and_keeps_a_lining():
    big = box((0.5, 0.5, 0.5), (1.0, 1.0, 1.0), "timber")
    sliver = box((1.01, 0.5, 0.5), (0.015, 0.8, 0.8), "timber")
    lining = box((1.01, 0.5, 0.5), (0.02, 0.8, 0.8), "masonry")
    kept = slabs.prune([big, sliver, lining], lambda b: b["m"], 0.02)
    assert big in kept and lining in kept and sliver not in kept
    far = box((1.2, 0.5, 0.5), (0.02, 0.8, 0.8), "timber")
    assert far in slabs.prune([big, far], lambda b: b["m"], 0.02), "10 cm off is no sliver"


def test_prune_cannot_erode_a_staircase_step_by_step():
    # a spike's staircase: each step 1 cm longer than the next, all 1 cm apart; dropping one may not license dropping
    # the one that covered it, or the tip would erode by a centimetre per step
    steps = [box((0.5 + 0.005 * k, 0.005 + 0.01 * k, 0.0), (1.0 - 0.01 * k, 0.01, 0.01), "timber") for k in range(40)]
    kept = slabs.prune(steps, lambda b: b["m"], 0.02)
    for k in range(40):
        p = (0.5 + 0.005 * k, 0.005 + 0.01 * k, 0.0)
        assert any(slabs._inside(p, slabs._frame(b), 0.02) for b in kept), f"step {k} left uncovered"


def test_prune_keeps_a_strip_of_a_sheet_whose_neighbours_only_touch_it():
    # three 2 cm strips of one 2 cm sheet, stacked edge to edge: the middle one's surface lies within 2 cm of the
    # others, but dropping it would leave a 2 cm slit through the sheet
    strips = [dict(box((0.5, 0.01 + 0.02 * k, 0.0), (1.0, 0.02, 0.02), "timber"), normal=(0.0, 0.0, 1.0))
              for k in range(3)]
    assert len(slabs.prune(strips, lambda b: b["m"], 0.02)) == 3
    behind = dict(box((0.5, 0.03, 0.025), (1.0, 0.06, 0.02), "timber"), normal=(0.0, 0.0, 1.0))
    kept = slabs.prune(strips + [behind], lambda b: b["m"], 0.02)
    assert len(kept) < 4, "a box right behind a strip along the normal closes its slit: the strip may go"
    for y in (0.001, 0.01, 0.019, 0.021, 0.03, 0.039, 0.041, 0.05, 0.059):
        assert any(slabs._segment_meets((0.5, y, 0.0), (0.0, 0.0, 1.0), 0.03, slabs._frame(b)) for b in kept), y


def test_a_seeded_run_keeps_the_seeds_families():
    parts = [slabs.Part("wall", "timber")]
    tris = wall_face(0.2, (1, 0, 0), 0) + wall_face(0.0, (-1, 0, 0), 0)
    first = slabs.decompose(tris, parts, SETTINGS)
    seed = [f.n for f in first["families"]]
    again = slabs.decompose(tris[:4], parts, SETTINGS, seed)
    assert {f.key for f in again["families"]} <= {f.key for f in first["families"]}


def caps(x0, x1, part):
    """The faces that close the space between wall_face(x0) and wall_face(x1): the top, the two ends and the
    window's four reveals, each facing out of the solid."""
    out = quad((x0, 3, 0), (x1, 3, 0), (x1, 3, 4), (x0, 3, 4), (0, 1, 0), part)
    out += quad((x0, 0, 0), (x1, 0, 0), (x1, 3, 0), (x0, 3, 0), (0, 0, -1), part)
    out += quad((x0, 0, 4), (x1, 0, 4), (x1, 3, 4), (x0, 3, 4), (0, 0, 1), part)
    out += quad((x0, 1, 1.5), (x1, 1, 1.5), (x1, 2, 1.5), (x0, 2, 1.5), (0, 0, 1), part)
    out += quad((x0, 1, 2.5), (x1, 1, 2.5), (x1, 2, 2.5), (x0, 2, 2.5), (0, 0, -1), part)
    out += quad((x0, 1, 1.5), (x1, 1, 1.5), (x1, 1, 2.5), (x0, 1, 2.5), (0, 1, 0), part)
    out += quad((x0, 2, 1.5), (x1, 2, 1.5), (x1, 2, 2.5), (x0, 2, 2.5), (0, -1, 0), part)
    return out


def test_two_skins_the_outside_reaches_between_are_two_sheets_open_between_them():
    parts = [slabs.Part("wall", "timber"), slabs.Part("wall", "masonry", layer_m=0.02)]
    tris = wall_face(0.2, (1, 0, 0), 0) + wall_face(0.0, (-1, 0, 0), 1)
    out = slabs.decompose(tris, parts, SETTINGS, air=air.Air(tris, 0.02))
    boxes = out["boxes"]
    assert not covers(boxes, (0.1, 1.5, 0.5)), "open between the skins"
    for p in ((0.01, 1.5, 0.5), (0.19, 1.5, 0.5), (0.01, 2.9, 3.9), (0.19, 0.1, 0.1)):
        assert covers(boxes, p)
    assert sorted({(round(b["lo"], 9), round(b["hi"], 9), parts[b["part"]].physics) for b in boxes}) == [
        (0.0, 0.02, "masonry"), (0.18, 0.2, "timber")]
    volume = sum(b["size"][0] * b["size"][1] * b["size"][2] for b in boxes)
    assert volume == pytest.approx(2 * 0.02 * (12 - 1), rel=1e-6)
    shape = {k for region in out["regions"] for k in region}
    assert sorted((round(lo, 9), round(hi, 9), pi) for lo, hi, pi in shape) == [(0.0, 0.02, 1), (0.02, 0.2, 0)],         "the wall's shape still spans both skins"


def test_two_skins_closed_all_round_stay_one_solid_wall():
    parts = [slabs.Part("wall", "timber"), slabs.Part("wall", "masonry", layer_m=0.02)]
    tris = wall_face(0.2, (1, 0, 0), 0) + wall_face(0.0, (-1, 0, 0), 1) + caps(0.0, 0.2, 0)
    boxes = slabs.decompose(tris, parts, SETTINGS, air=air.Air(tris, 0.02))["boxes"]
    assert covers(boxes, (0.1, 1.5, 0.5)), "the ground and the caps close it: solid"
    assert not covers(boxes, (0.1, 1.5, 2.0)), "the window stays clear"
    assert any(b["size"][0] == pytest.approx(0.18) for b in boxes)


def test_a_pair_too_narrow_for_air_between_its_sheets_stays_whole():
    parts = [slabs.Part("leaf", "timber", sheet_m=0.02)]
    tris = wall_face(0.05, (1, 0, 0), 0) + wall_face(0.0, (-1, 0, 0), 0)  # 5 cm: 1 cm left between 2 cm sheets
    boxes = slabs.decompose(tris, parts, SETTINGS, air=air.Air(tris, 0.02))["boxes"]
    assert covers(boxes, (0.025, 1.5, 0.5))
