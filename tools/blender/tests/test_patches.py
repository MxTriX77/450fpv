"""lib.patches, the bare soil patches derive.py and ground_patch share (p1-s04-belt-straw, review-physics PR4 and PR5):
the S04 layout's outlines are convex with no turn over 90 degrees, measure inside the size range along and across
their angle, lie near the lodging azimuth and cover the bare share within the tolerance; the layout is seeded;
every window minus its outline is covered by the patch's edge boxes (the self-check, and an independent sample);
no edge box leaves the area or enters another outline; the fill rectangles and the windows cover the area while no
fill rectangle comes near an outline; the core is the outline shrunk by half the feather."""
import math

import pytest

from lib import patches


@pytest.fixture(scope="module")
def layout(s04_params):
    return patches.layout(**patches.inputs(s04_params))


def grid_points(x0, x1, z0, z1, step):
    nx, nz = max(1, math.ceil((x1 - x0) / step)), max(1, math.ceil((z1 - z0) / step))
    return [(x0 + (x1 - x0) * i / nx, z0 + (z1 - z0) * j / nz) for i in range(nx + 1) for j in range(nz + 1)]


def test_outlines_are_convex_sized_and_cover_the_share(layout, s04_params):
    bare, size = s04_params["bare"], s04_params["size_m"]
    print(f"S04: {len(layout['patches'])} bare patches, share {layout['share']:.4f} on their outlines")
    assert abs(layout["share"] - bare["share"]) <= patches.SHARE_TOLERANCE
    assert sum(patches.area(p["outline"]) for p in layout["patches"]) / size ** 2 == pytest.approx(layout["share"])
    for p in layout["patches"]:
        assert len(p["outline"]) == bare["outline_vertices"] and patches.area(p["outline"]) > 0
        assert all(0 < t <= math.radians(patches.MAX_EXTERIOR_DEG) + 1e-12 for t in patches._turns(p["outline"]))
        a = math.radians(p["angle_deg"])
        along = [x * math.cos(a) + z * math.sin(a) for x, z in p["outline"]]
        across = [-x * math.sin(a) + z * math.cos(a) for x, z in p["outline"]]
        length, width = max(along) - min(along), max(across) - min(across)
        assert bare["size_m"]["min"] - 1e-9 <= width <= length + 1e-9 <= bare["size_m"]["max"] + 1e-9, (length, width)
        assert abs(p["angle_deg"] - s04_params["lodging"]["azimuth_deg"]) <= patches.ANGLE_SPREAD_DEG


def test_the_layout_is_seeded(layout, s04_params):
    inputs = patches.inputs(s04_params)
    assert patches.layout(**inputs) == layout
    assert patches.layout(**dict(inputs, seed=inputs["seed"] + 1))["patches"][0]["outline"] != layout["patches"][0]["outline"]


def test_edge_boxes_cover_each_window_outside_its_outline(layout):
    """PR4's self-check, and an independent 5 cm sample of each window against the boxes as corner polygons."""
    for k, p in enumerate(layout["patches"]):
        assert patches.uncovered(p) == [], f"patch {k}"
        rects = [patches.rect_corners(r) for r in p["edges"]]
        for q in grid_points(*p["window"], 0.05):
            if patches.distance(q, p["outline"]) > 1e-9:
                assert any(patches.distance(q, r) <= 1e-9 for r in rects), f"patch {k}: {q} lies in no edge box"
        assert all(r["s"][0] == 0.0 and r["s"][1] >= patches.REACH_M for r in p["edges"])


def test_edge_boxes_stay_in_the_area_and_out_of_other_outlines(layout, s04_params):
    half = s04_params["size_m"] / 2
    bare = layout["patches"]
    for k, p in enumerate(bare):
        for r in p["edges"]:
            corners = patches.rect_corners(r)
            assert all(abs(c) <= half + 1e-9 for corner in corners for c in corner), f"patch {k} leaves the area"
            for j, other in enumerate(bare):
                if j != k:
                    inner = [q for q in grid_points(*other["window"], 0.05) if patches.distance(q, other["outline"]) < -1e-6]
                    assert not any(patches.distance(q, corners) < -1e-6 for q in inner), f"patch {k} enters patch {j}"


def test_fill_and_windows_cover_the_area_and_fill_keeps_off_the_outlines(layout, s04_params):
    size = s04_params["size_m"]
    rects = patches.fill(size, layout["patches"])
    windows = [p["window"] for p in layout["patches"]]

    def inside(q, r):
        return r[0] - 1e-9 <= q[0] <= r[1] + 1e-9 and r[2] - 1e-9 <= q[1] <= r[3] + 1e-9

    for q in grid_points(-size / 2, size / 2, -size / 2, size / 2, 0.05):
        assert any(inside(q, r) for r in rects) or any(inside(q, w) for w in windows), q
    keep = patches.WINDOW_M - patches.SEAM_M
    for r in rects:
        box = [(r[0], r[2]), (r[1], r[2]), (r[1], r[3]), (r[0], r[3])]
        for p in layout["patches"]:
            assert all(patches.distance(c, box) >= keep - 1e-9 for c in p["outline"])
            assert all(patches.distance(c, p["outline"]) >= keep - 1e-9 for c in box)


def test_the_core_is_the_outline_shrunk_by_half_the_feather(layout):
    square = [(0.0, 0.0), (1.0, 0.0), (1.0, 1.0), (0.0, 1.0)]
    shrunk = patches.inset(square, 0.1)
    expected = [(0.1, 0.1), (0.9, 0.1), (0.9, 0.9), (0.1, 0.9)]
    assert len(shrunk) == 4 and all(any(math.dist(a, b) < 1e-12 for a in shrunk) for b in expected), shrunk
    assert patches.inset(square, 0.6) == []
    for p in layout["patches"]:
        assert p["core"] == patches.inset(p["outline"], patches.FEATHER_M / 2)
        for q in p["core"]:
            assert patches.distance(q, p["outline"]) == pytest.approx(-patches.FEATHER_M / 2, abs=1e-9) or \
                patches.distance(q, p["outline"]) < -patches.FEATHER_M / 2
