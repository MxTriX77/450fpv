"""generators.debris_brick: exact dimensions (AST-04), the chamfer wear within its params, determinism per seed,
outward normals, every face UV'd into one brick cell of the texture at a uniform fit with V up and the chamfers
continuing their faces (AST-07), and a params edit that moves the bounds and the collision box with it (AST-03)."""
import copy

import pytest

from generators import debris_brick

MM = 1e-3
EPS = 1e-9


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


def test_every_face_maps_into_the_brick_cell_at_a_uniform_centred_fit(k01_params):
    (u0, v0), (u1, v1) = debris_brick.CELL_M
    assert (u0, v0, u1, v1) == pytest.approx((388 / 1024 * 1.4, 571 / 1024 * 1.4, 533 / 1024 * 1.4, 601 / 1024 * 1.4))
    built = debris_brick.build(k01_params)
    for mesh in built["lods"]:
        for _, _, uvs, _ in mesh.faces:
            for u, v in uvs:
                assert u0 - EPS <= u <= u1 + EPS and v0 - EPS <= v <= v1 + EPS, f"{(u, v)} leaves the cell"
    lod1 = built["lods"][1]
    face_size = {(0, 0, 1): (0.25, 0.065), (1, 0, 0): (0.12, 0.065), (0, 1, 0): (0.25, 0.12)}  # stretcher, header, bed
    for indices, normal, uvs, _ in lod1.faces:
        fw, fh = face_size[tuple(abs(c) for c in normal)]
        scale = min((u1 - u0) / fw, (v1 - v0) / fh)
        us, vs = [u for u, _ in uvs], [v for _, v in uvs]
        assert (max(us) - min(us), max(vs) - min(vs)) == pytest.approx((fw * scale, fh * scale))
        assert ((min(us) + max(us)) / 2, (min(vs) + max(vs)) / 2) == pytest.approx(((u0 + u1) / 2, (v0 + v1) / 2))
        if normal[1] == 0:  # side and end faces are upright: the top edge has the larger V
            for i, (_, v) in zip(indices, uvs):
                assert (v > (v0 + v1) / 2) == (lod1.vertices[i][1] > 0.03)
    stretcher = min((u1 - u0) / 0.25, (v1 - v0) / 0.065)
    assert stretcher == pytest.approx(0.631, abs=0.001) and min((u1 - u0) / 0.25, (v1 - v0) / 0.12) == pytest.approx(0.342, abs=0.001)

    lod0 = built["lods"][0]
    by_normal = {normal: dict(zip(indices, uvs)) for indices, normal, uvs, _ in lod0.faces}
    side, strip = by_normal[(0.0, 0.0, 1.0)], by_normal[(0.0, 1.0, 1.0)]  # the +Z face and its top chamfer strip
    # The strip continues the +Z face's map: the two vertices they share carry the same UV, and the strip climbs
    # from there to y = height, the cell's top (the stretcher fills the cell's height), inside the face's U range.
    shared = set(side) & set(strip)
    assert len(shared) == 2
    for i in shared:
        assert strip[i] == pytest.approx(side[i])
    assert max(v for _, v in strip.values()) == pytest.approx(v1) and max(v for _, v in side.values()) < v1 - MM
    assert min(u for u, _ in side.values()) <= min(u for u, _ in strip.values())
    assert max(u for u, _ in strip.values()) <= max(u for u, _ in side.values())


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
