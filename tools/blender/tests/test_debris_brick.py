"""generators.debris_brick: exact dimensions (AST-04), the chamfer wear within its params, determinism per seed,
outward normals, and a params edit that moves the bounds and the collision box with it (AST-03)."""
import copy

import pytest

from generators import debris_brick

MM = 1e-3


def test_lod0_bounds_are_the_published_brick(k01_params):
    lo, hi = debris_brick.build(k01_params)["lods"][0].bounds()
    assert lo == pytest.approx((-0.125, 0.0, -0.06), abs=1e-9)
    assert hi == pytest.approx((0.125, 0.065, 0.06), abs=1e-9)


def test_counts(k01_params):
    lod0, lod1 = debris_brick.build(k01_params)["lods"]
    assert (len(lod0.vertices), len(lod0.faces), lod0.triangles()) == (24, 26, 44)
    assert (len(lod1.vertices), len(lod1.faces), lod1.triangles()) == (8, 6, 12)
    assert lod1.bounds() == lod0.bounds()


def test_every_lod0_vertex_is_pulled_in_by_a_chamfer_within_params(k01_params):
    bevel = k01_params["bevel_mm"]
    lo, hi = (-0.125, 0.0, -0.06), (0.125, 0.065, 0.06)
    for v in debris_brick.build(k01_params)["lods"][0].vertices:
        pulls = [min(abs(v[a] - lo[a]), abs(hi[a] - v[a])) for a in range(3)]
        on_face = [p < 1e-12 for p in pulls]
        assert on_face.count(True) == 1, f"{v} should sit on exactly one face"
        for p, flat in zip(pulls, on_face):
            if not flat:
                assert bevel["min"] * MM - 1e-12 <= p <= bevel["max"] * MM + 1e-12


def test_seed_determines_the_wear(k01_params):
    a = debris_brick.build(k01_params)["lods"][0].vertices
    assert a == debris_brick.build(k01_params)["lods"][0].vertices
    other = dict(k01_params, seed=k01_params["seed"] + 1)
    b = debris_brick.build(other)["lods"][0].vertices
    assert a != b
    assert debris_brick.build(other)["lods"][0].bounds() == debris_brick.build(k01_params)["lods"][0].bounds()


def test_face_normals_point_outward(k01_params):
    for mesh in debris_brick.build(k01_params)["lods"]:
        centre = (0.0, 0.0325, 0.0)
        for indices, normal, uvs, slot in mesh.faces:
            centroid = [sum(mesh.vertices[i][a] for i in indices) / len(indices) for a in range(3)]
            assert sum((centroid[a] - centre[a]) * normal[a] for a in range(3)) > 0
            assert len(uvs) == len(indices) and slot == "brick"


def test_height_edit_moves_bounds_and_collision(k01_params):
    taller = copy.deepcopy(k01_params)
    taller["size_m"]["height"] = 0.088
    built = debris_brick.build(taller)
    assert built["lods"][0].bounds()[1][1] == pytest.approx(0.088)
    box = built["collision"][0]
    assert box["size"] == pytest.approx([0.25, 0.088, 0.12])
    assert box["center"] == pytest.approx([0.0, 0.044, 0.0])
    assert box["material"] == "masonry" and box["landable"] is False
    assert built["gaps"] == [] and built["wind_volumes"] == [] and built["snag"] == []
