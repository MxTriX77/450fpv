"""The vendor pieces' physics (w4b-farmland-ruins): their params against the generator, the committed metadata
against schema 1 and the physics table, and, where the local store is linked, a rebuild byte for byte."""
import itertools
import json
import math
import os

import pytest

from generators import vendor_ruin
from lib import catalog, meta, params as params_lib

import vendor_build

MODELS = ("unity", "Assets", "_Project", "Art", "Models", "ruins")


def variants(repo_root):
    assets = vendor_build.load(str(repo_root))
    return assets, [(a, v) for a in sorted(assets) for v in assets[a]["variants"]]


def load(repo_root, asset_id, variant):
    path = repo_root.joinpath(*MODELS, asset_id, variant + ".assetmeta.json")
    return json.loads(path.read_text(encoding="utf-8")), path


def test_every_variant_has_params_the_generator_accepts(repo_root):
    assets, pairs = variants(repo_root)
    assert len(pairs) == 13
    for asset_id, variant in pairs:
        params = catalog.load_params(catalog.params_path(str(repo_root), assets[asset_id], variant))
        assert params_lib.validate(vendor_ruin, params) == [], variant


def test_committed_metadata_validates_and_names_every_shapes_part(repo_root, material_ids):
    _, pairs = variants(repo_root)
    for asset_id, variant in pairs:
        data, path = load(repo_root, asset_id, variant)
        assert meta.validate(data, material_ids) == [], variant
        assert data["vendor"]["object"] in variant
        assert data["collision"] and all(c.get("part") for c in data["collision"]), variant
        assert {c["material"] for c in data["collision"]} <= {"timber", "masonry", "asbestos_cement"}, variant
        ids = [g["id"] for g in data["gaps"]]
        assert len(ids) == len(set(ids)), f"{variant}: gap ids are unique"
        assert os.path.exists(str(path) + ".meta"), "Unity's .meta beside the metadata"


def test_the_ruins_keep_their_walls_openings_and_floor(repo_root):
    data, _ = load(repo_root, "N01", "Ruin_RoofCollapsed")
    parts = {c["part"] for c in data["collision"]}
    assert {"wall_back", "wall_front", "wall_stove_end", "wall_far_end", "partition", "floor", "roof", "ceiling",
            "window_frame", "chimney", "stove"} <= parts
    assert all(c["landable"] == (c["part"] == "floor") for c in data["collision"])
    assert any(g["id"].startswith("wall_back_window") for g in data["gaps"])
    assert any(g["id"].startswith("roof_hole") for g in data["gaps"]), "the collapse holes the roof"
    assert any(w.get("cavity") for w in data["wind_volumes"])
    walls, _ = load(repo_root, "N01", "Ruin_Walls")
    assert not any(c["part"] in ("roof", "ceiling") for c in walls["collision"]), "the roofless ruin keeps no roof"


def test_a_broken_wall_is_its_two_skins_open_between_them(repo_root):
    # the back wall of the collapsed-roof ruin at x -3.0, 1.5 m up (the sim test's crash point): the siding's face at
    # z -4.552 and the plaster's at -4.352, the outside reaching the space between them through the broken tops
    data, _ = load(repo_root, "N01", "Ruin_RoofCollapsed")

    def parts_at(z):
        out = []
        for c in data["collision"]:
            lo = [c["center"][k] - c["size"][k] / 2 for k in range(3)]
            hi = [c["center"][k] + c["size"][k] / 2 for k in range(3)]
            if c["rotation"] == [0.0, 0.0, 0.0, 1.0] and all(lo[k] <= p <= hi[k] for k, p in enumerate((-3.0, 1.51, z))):
                out.append((c["part"], c["material"]))
        return out

    assert parts_at(-4.545) == [("wall_back", "timber")], "the siding"
    assert parts_at(-4.36) == [("wall_back", "masonry")], "the plaster"
    for z in (-4.52, -4.45, -4.38):
        assert parts_at(z) == [], f"open between the skins at z {z}"


def test_the_mirrored_ruin_is_the_ruin_mirrored(repo_root):
    a, _ = load(repo_root, "N01", "Ruin_Walls")
    b, _ = load(repo_root, "N01", "Ruin_Walls_mirrored")
    assert b["bounds"]["min"][0] == pytest.approx(-a["bounds"]["max"][0])
    assert b["bounds"]["max"][0] == pytest.approx(-a["bounds"]["min"][0])
    assert len(b["collision"]) == pytest.approx(len(a["collision"]), rel=0.05)
    ga, gb = {g["id"]: g for g in a["gaps"]}, {g["id"]: g for g in b["gaps"]}
    for gid in set(ga) & set(gb):  # one id, one opening: numbered in the source mesh's frame
        ca = [sum(p[k] for p in ga[gid]["polygon"]) / 4 for k in range(3)]
        cb = [sum(p[k] for p in gb[gid]["polygon"]) / 4 for k in range(3)]
        assert (cb[0], cb[1], cb[2]) == pytest.approx((-ca[0], ca[1], ca[2]), abs=0.03), gid
    for gid in set(ga) ^ set(gb):  # a pane cut to within a few cm of the smallest gap may fall either side of it
        g = ga.get(gid) or gb.get(gid)
        assert g["narrowest_m"] < 0.25 + 0.05, f"{gid} is in one of the two only"
    assert len(set(ga) & set(gb)) >= 0.8 * max(len(ga), len(gb))


def test_unity_euler_turns_x_onto_y_about_z():
    q = vendor_ruin.unity_euler([0.0, 0.0, 90.0])
    assert vendor_ruin._rotate(q, (1.0, 0.0, 0.0)) == pytest.approx((0.0, 1.0, 0.0), abs=1e-12)


def test_classify_takes_the_first_matching_rule():
    rules = [{"part": "floor", "physics": "timber", "normal": "up", "box": [[-1, -1, -1], [1, 0.6, 1]]},
             {"part": "roof", "physics": "asbestos_cement", "slots": ["Slate"], "normal": "sloped"},
             {"part": "trim", "physics": "timber"}]
    assert vendor_ruin.classify(rules, "Wood", (0, 1, 0), (0, 0.4, 0)) == 0
    assert vendor_ruin.classify(rules, "Slate_Burnt", (0, 0.8, 0.6), (0, 5, 0)) == 1, "a burnt copy is its slot"
    assert vendor_ruin.classify(rules, "Wood", (0, 0.8, 0.6), (0, 5, 0)) == 2


@pytest.mark.parametrize("name", ["N02", "Ruin_Stump"])
def test_a_rebuild_reproduces_the_committed_files(repo_root, name, capsys):
    if not os.path.exists(os.path.join(str(repo_root), "vendor", "world_final")):
        pytest.skip("the local store is not linked here (pwsh tools/vendor/link.ps1)")
    assert vendor_build.main([name, "--check"]) == 0
    assert "DIFFERS" not in capsys.readouterr().out


def _rotated(q, v):
    x, y, z, w = q
    t = (2 * (y * v[2] - z * v[1]), 2 * (z * v[0] - x * v[2]), 2 * (x * v[1] - y * v[0]))
    return (v[0] + w * t[0] + y * t[2] - z * t[1], v[1] + w * t[1] + z * t[0] - x * t[2],
            v[2] + w * t[2] + x * t[1] - y * t[0])


def _box_corners(shape):
    half = [s / 2 for s in shape["size"]]
    return [tuple(c + d for c, d in zip(shape["center"], _rotated(shape["rotation"], (sx * half[0], sy * half[1],
                                                                                         sz * half[2]))))
            for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)]


def test_no_pushed_point_is_a_vendor_vertex(repo_root):
    """D-089 (task 1.3, QA's F1): every point the committed metadata pushes (each snag point, gap corner, corner of a
    collision box or a wind volume, and corner of the bounds) lies more than 0.1 mm, on some axis, from every vertex
    of its vendor mesh, read through the generator's own reader and placement. Needs the local store."""
    if not os.path.exists(os.path.join(str(repo_root), "vendor", "world_final")):
        pytest.skip("the local store is not linked here (pwsh tools/vendor/link.ps1)")
    tol = 1e-4
    assets, pairs = variants(repo_root)
    files, found = {}, []
    for asset_id, variant in pairs:
        params = catalog.load_params(catalog.params_path(str(repo_root), assets[asset_id], variant))
        src = params["source"]
        if src["model"] not in files:
            files[src["model"]] = vendor_ruin._read(src["model"], src["model_sha256"])
        mesh = files[src["model"]][src["object"]]
        place = vendor_ruin._placement(mesh["vertices"], params["prefab"])
        lod0 = vendor_ruin._triangles(mesh, params["parts"], place, params["prefab"]["mirror_x"])[1]
        cells = {}
        for v in lod0.vertices:
            cells.setdefault(tuple(math.floor(c / tol) for c in v), []).append(v)
        data, _ = load(repo_root, asset_id, variant)
        lo, hi = data["bounds"]["min"], data["bounds"]["max"]
        pushed = ([("snag", p) for s in data["snag"] for p in s["points"]]
                  + [("gap", p) for g in data["gaps"] for p in g["polygon"]]
                  + [("box", p) for c in data["collision"] for p in _box_corners(c)]
                  + [("wind", p) for w in data["wind_volumes"] for p in _box_corners(w)]
                  + [("bounds", (x, y, z)) for x in (lo[0], hi[0]) for y in (lo[1], hi[1]) for z in (lo[2], hi[2])])
        for field, p in pushed:
            k = [math.floor(c / tol) for c in p]
            for d in itertools.product((-1, 0, 1), repeat=3):
                for v in cells.get((k[0] + d[0], k[1] + d[1], k[2] + d[2]), ()):
                    if max(abs(a - b) for a, b in zip(v, p)) <= tol:
                        found.append(f"{variant} {field} {[round(c, 6) for c in p]}")
    assert found == [], f"{len(found)} pushed points on a vendor vertex: {found[:10]}"


def _axes(q):
    return [_rotated(q, e) for e in ((1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0))]


def _boxes_meet(a, b):
    """Two boxes (center, axes, half sizes) overlap: no separating axis among their faces' and edges' normals."""
    def cross(u, v):
        return (u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0])

    def dot(u, v):
        return u[0] * v[0] + u[1] * v[1] + u[2] * v[2]
    d = [b[0][k] - a[0][k] for k in range(3)]
    for axis in a[1] + b[1] + [cross(u, v) for u in a[1] for v in b[1]]:
        if dot(axis, axis) < 1e-12:
            continue
        ra = sum(h * abs(dot(axis, e)) for h, e in zip(a[2], a[1]))
        rb = sum(h * abs(dot(axis, e)) for h, e in zip(b[2], b[1]))
        if abs(dot(d, axis)) >= ra + rb - 1e-9 * math.sqrt(dot(axis, axis)):
            return False
    return True


def test_every_gap_is_clear_space_in_the_collision(repo_root):
    """A gap is the real clear space (REAL-05; task 1.3, QA's F3 and F4): its polygon, 2 mm in from its edges, swept
    0.2 m either side of its plane (QA's clear prism), meets no collision box of the committed metadata: no kept roof
    member across a roof hole, no rail across a pane's foot."""
    _, pairs = variants(repo_root)
    found = []
    for asset_id, variant in pairs:
        data, _ = load(repo_root, asset_id, variant)
        boxes = [(c["center"], _axes(c["rotation"]), [s / 2 for s in c["size"]]) for c in data["collision"]]
        for gap in data["gaps"]:
            pts = gap["polygon"]
            centre = [sum(p[k] for p in pts) / len(pts) for k in range(3)]
            u = [pts[1][k] - pts[0][k] for k in range(3)]
            u = [c / math.sqrt(sum(x * x for x in u)) for c in u]
            n = gap["normal"]
            v = [n[1] * u[2] - n[2] * u[1], n[2] * u[0] - n[0] * u[2], n[0] * u[1] - n[1] * u[0]]
            half = [max(abs(sum((p[k] - centre[k]) * e[k] for k in range(3))) for p in pts) - 0.002 for e in (u, v)]
            prism = (centre, [u, v, n], half + [0.2])
            for i, box in enumerate(boxes):
                if math.dist(box[0], centre) <= math.hypot(*box[2]) + math.hypot(*prism[2]) and _boxes_meet(prism, box):
                    found.append(f"{variant} {gap['id']}: collision[{i}] ({data['collision'][i]['part']})")
    assert found == [], found[:10]


DRONE_SPAN_M = 0.574  # the drone's span tip to tip (design §5), the capsule's diameter
SWEEP_M = 0.25  # the capsule's centre from this far before a gap's plane to this far past it: a 0.24 m wall and its
# frame band (gaps.band_m, 0.12 m) lie within the sphere at both ends


def _segment_box_distance(a, b, shape):
    """The least distance between segment ab and a (rotated) box: the distance to a convex set is convex along the
    segment, so a golden-section search finds it."""
    inverse = [-shape["rotation"][0], -shape["rotation"][1], -shape["rotation"][2], shape["rotation"][3]]
    half = [s / 2 for s in shape["size"]]

    def distance(t):
        p = [a[k] + t * (b[k] - a[k]) - shape["center"][k] for k in range(3)]
        local = _rotated(inverse, p)
        return math.sqrt(sum(max(abs(local[k]) - half[k], 0.0) ** 2 for k in range(3)))
    lo, hi = 0.0, 1.0
    golden = (math.sqrt(5) - 1) / 2
    for _ in range(60):
        m1, m2 = hi - golden * (hi - lo), lo + golden * (hi - lo)
        if distance(m1) <= distance(m2):
            hi = m2
        else:
            lo = m1
    return min(distance(0.0), distance(1.0), distance((lo + hi) / 2))


def test_a_drone_sized_capsule_passes_every_drone_sized_gap(repo_root):
    """AST-05's acceptance (task 1.3, QA's F6): for every gap at least the drone's span either way, a capsule of the
    drone's diameter along the gap's normal through its centre, its centre swept SWEEP_M either side of the plane,
    touches no collision box of the committed metadata."""
    _, pairs = variants(repo_root)
    blocked, flown = [], 0
    for asset_id, variant in pairs:
        data, _ = load(repo_root, asset_id, variant)
        for gap in data["gaps"]:
            if gap["narrowest_m"] < DRONE_SPAN_M:
                continue
            flown += 1
            centre = [sum(p[k] for p in gap["polygon"]) / len(gap["polygon"]) for k in range(3)]
            n = gap["normal"]
            a = [centre[k] - SWEEP_M * n[k] for k in range(3)]
            b = [centre[k] + SWEEP_M * n[k] for k in range(3)]
            reach = SWEEP_M + DRONE_SPAN_M / 2
            for i, shape in enumerate(data["collision"]):
                extent = math.sqrt(sum((s / 2) ** 2 for s in shape["size"]))
                if math.dist(shape["center"], centre) > reach + extent:
                    continue
                clearance = _segment_box_distance(a, b, shape) - DRONE_SPAN_M / 2
                if clearance <= 0:
                    blocked.append(f"{variant} {gap['id']}: collision[{i}] ({shape['part']}) {clearance * 1000:.1f} mm")
    assert flown >= 40, "the ruins' doors and the collapse's large holes"
    assert blocked == [], blocked[:10]
