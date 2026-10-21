"""lib.kits_vehicle: rigid transforms of points, normals and shapes, the body settled on its springs, the burst's
direction, the hole sampler's sizes and room, a hole's patch stitched into a sheet (closed, its rim an edge snag, its
petals bent the measured 10-30 mm), and the scatter's 1/r^2 distances (p1-r01-vaz-sedan, design decisions 3 and 7)."""
import math
import random

import pytest

from lib import kits_vehicle as kits, loft
from lib.geometry import Mesh


def test_rigid_transforms_compose_and_move_shapes():
    rot = kits.axis_rotation((1.0, 0.0, 0.0), (0.0, 1.0, 0.0), math.pi / 2)
    assert rot.point((2.0, 0.0, 0.0)) == pytest.approx((1.0, 0.0, -1.0))
    move = rot.then(kits.translation((0.0, 1.0, 0.0)))
    assert move.point((1.0, 0.0, 0.0)) == pytest.approx((1.0, 1.0, 0.0))
    box = {"shape": "box", "center": [2.0, 0.0, 0.0], "size": [1, 1, 1], "rotation": [0.0, 0.0, 0.0, 1.0]}
    moved = kits.transform_shape(box, move)
    assert moved["center"] == pytest.approx([1.0, 1.0, -1.0])
    s = math.sin(math.pi / 4)
    assert moved["rotation"] == pytest.approx([0.0, s, 0.0, s])
    assert kits.Rigid().is_identity() and not move.is_identity()


@pytest.mark.parametrize("flat, far", [("front_left", "rear_right"), ("rear_right", "front_left"),
                                       ("front_right", "rear_left")])
def test_lean_settles_the_body_on_four_equal_springs(flat, far):
    """QA 2.1's R1: on a rectangle the flat corner's support drop d moves the body d / 4 down at its centre and its
    corners -3d/4 (the flat one), -d/4, -d/4 and +d/4 (the opposite one): the plane through the supports'
    displacements, the four springs' balance of load and both moments. The warp is the springs'."""
    hubs = {"front_left": (-1.2, 0.28, -0.67), "front_right": (-1.2, 0.28, 0.67), "rear_left": (1.2, 0.28, -0.67),
            "rear_right": (1.2, 0.28, 0.67)}
    d = 0.05
    rigid = kits.lean(hubs, flat, d)
    moved = {c: rigid.point(h)[1] - h[1] for c, h in hubs.items()}
    want = {c: -d / 4 for c in hubs} | {flat: -3 * d / 4, far: d / 4}
    assert moved == pytest.approx(want, abs=1e-5)  # second order: the tilted points shift 0.01 mm in plan
    assert rigid.point((0.0, 0.28, 0.0))[1] - 0.28 == pytest.approx(-d / 4, abs=1e-9)
    assert sum(moved[c] - (-d if c == flat else 0.0) for c in hubs) == pytest.approx(0.0, abs=1e-5)  # load balance
    up = rigid.vector((0.0, 1.0, 0.0))  # a rigid tilt: half the turn about the diagonal
    assert math.degrees(math.acos(up[1])) == pytest.approx(
        math.degrees(math.atan(math.hypot(d / 2 / 1.34, d / 2 / 2.4))), abs=1e-3)


def test_burst_vector_bearings():
    assert kits.burst_vector(0.0) == pytest.approx((-1.0, 0.0, 0.0))  # ahead of the nose (-X)
    assert kits.burst_vector(90.0) == pytest.approx((0.0, 0.0, 1.0), abs=1e-12)  # the right side (+Z)
    assert kits.burst_vector(270.0) == pytest.approx((0.0, 0.0, -1.0), abs=1e-12)
    assert kits.burst_vector(0.0, 90.0) == pytest.approx((0.0, 1.0, 0.0), abs=1e-12)


class Region:
    bounds = (0.0, 3.0, 0.0, 1.0)

    def __call__(self, s, y, r):
        return r <= s <= 3.0 - r and r <= y <= 1.0 - r


def test_sampled_holes_sizes_and_room():
    holes = kits.sample_holes(4, 6, 0.03, 1.8, -1, random.Random(3), Region())
    large = [h for h in holes if h.large]
    assert len(holes) == 10 and len(large) == 4
    assert all(kits.LARGE_MIN_M <= h.d <= kits.LARGE_MAX_M and h.cut for h in large)
    assert all(kits.SMALL_MIN_M <= h.d <= 0.03 for h in holes if not h.large)
    for a in holes:
        for b in holes:
            if a is not b:
                assert max(abs(a.s - b.s), abs(a.y - b.y)) >= a.extent + b.extent + 0.02 - 1e-9
    with pytest.raises(ValueError, match="no room"):
        kits.sample_holes(60, 0, 0.03, 1.8, -1, random.Random(3), Region())


def test_patch_closes_the_sheet_round_a_petalled_hole():
    """A 0.15 m hole in a flat sheet: the quads of its square removed, the patch from their border to the jagged
    rim, the rim a closed edge snag, each petal's tip 10-30 mm behind the skin and its open centre within AST-05's
    5 cm of a tip; every snag names the sheet's physics material."""
    rng = random.Random(1)
    hole = kits.Hole(1, 0.5, 0.5, 0.15, 1.8, rng)
    step = 0.025
    grid = [[(i * step, j * step, 0.0) for j in range(41)] for i in range(41)]
    params = [[(p[0], p[1]) for p in row] for row in grid]
    r = hole.patch_radius()
    quads = {(i, j) for i in range(40) for j in range(40)
             if hole.s - r <= i * step and (i + 1) * step <= hole.s + r + 1e-9
             and hole.y - r <= j * step and (j + 1) * step <= hole.y + r + 1e-9}
    mask = [[(i, j) not in quads for j in range(40)] for i in range(40)]
    mesh = Mesh()
    loft.sheet(mesh, grid, [[(p[0], p[1]) for p in row] for row in grid], "paint", lambda p: (0.0, 0.0, 1.0),
               0.0008, mask, seams=quads)
    loop = kits.boundary_loop(quads)
    outer = [(grid[i][j], params[i][j], params[i][j]) for i, j in loop]

    def surf(s, y):
        return (s, y, 0.0), (0.0, 0.0, 1.0)
    snags = kits.emit_patch(mesh, hole, outer, surf, lambda s, y: (s, y), 0.0008, {"paint": "paint", "torn": "rust"},
                            "sheet_metal")
    assert snags[0]["points"][0] == snags[0]["points"][-1] and len(snags) == 1 + len(hole.petals)
    assert all(s["material"] == "sheet_metal" for s in snags), "the rim and every petal name the sheet's material"
    petal_points = [v for v in mesh.vertices if v[2] < -0.002]
    depths = [-v[2] for v in petal_points]
    assert max(depths) <= kits.PETAL_DEPTH_M[1] + 0.001 and max(depths) >= kits.PETAL_DEPTH_M[0] * 0.8
    tips = [v for v in petal_points if -v[2] > 0.008]
    assert min(math.hypot(v[0] - 0.5, v[1] - 0.5) for v in tips) <= kits.APERTURE_M


def test_scatter_radius_is_log_uniform_for_inverse_square():
    rng = random.Random(5)
    r = [kits.scatter_radius(rng, 1.0, 4.0) for _ in range(4000)]
    assert min(r) >= 1.0 and max(r) <= 4.0
    assert sum(d < 2.0 for d in r) / len(r) == pytest.approx(0.5, abs=0.03)
