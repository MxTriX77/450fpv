"""generators.shrub with lib.skeleton, lib.cards and lib.shell (p1-v13-shrub, "V13 belt understory shrub"): the
committed params conform and the defaults are V13_mid; the built sizes sit inside the sheet; LOD triangle counts
under budget and halving; every stem segment a bark capsule on its chord with a hook of half the base diameter; the
twig shell as at most 24 twig spheres and capsules; one wind box at porosity 0.35; no gap, nothing landable; the
skeleton's radii, tubes and normals; card UVs inside the atlas cells, the far cards over the whole atlas; sprig
cards leaving their twig at the fork angle; determinism per seed; check's refusals; the low variant through the
collision audit, and V13_mid's first, single sphere failing its standoff."""
import copy
import json
import math

import pytest

from generators import shrub
from lib import audit, cards, catalog, meta, params, shell, skeleton
from lib.geometry import Mesh

VARIANTS = ("V13_low", "V13_mid", "V13_tall")
SHEET = {"V13_low": ((1.0, 1.5), (1.0, 2.0)), "V13_mid": ((1.5, 2.5), (1.5, 3.0)), "V13_tall": ((2.5, 3.5), (2.0, 3.0))}
VISUALS = {"stem": "bark_shrub", "twig": "bark_shrub", "leaf": "leaf_shrub"}
UP = (0.0, 1.0, 0.0)
BUILT = {}  # one build per variant for the whole module: the shell fit takes seconds


def load(repo_root, variant):
    return json.loads((repo_root / "assets-src" / "vegetation" / f"{variant}.json").read_text(encoding="utf-8"))


def build(repo_root, name):
    """(name, params, built, metadata) of a committed variant, built once."""
    if name not in BUILT:
        p = load(repo_root, name)
        built = shrub.build(p)
        BUILT[name] = name, p, built, meta.assemble("V13", name, "shrub", p, built, VISUALS)
    return BUILT[name]


@pytest.fixture(params=VARIANTS)
def variant(request, repo_root):
    return build(repo_root, request.param)


def slot_vertices(mesh, slots):
    return sorted({mesh.vertices[i] for indices, _, _, slot in mesh.faces if slot in slots for i in indices})


def test_params_conform_and_the_defaults_are_the_mid_variant(repo_root):
    for name in VARIANTS:
        assert params.validate(shrub, load(repo_root, name)) == [], name
    flat = params._flatten(load(repo_root, "V13_mid"))
    assert set(flat) == set(shrub.PARAMS)
    for key, spec in shrub.PARAMS.items():
        assert spec["default"] == flat[key], key
        if spec["type"] in ("int", "float"):
            assert spec["range"][0] <= spec["default"] <= spec["range"][1], key


def test_sizes_inside_the_sheet(variant):
    name, p, built, data = variant
    (h_lo, h_hi), (w_lo, w_hi) = SHEET[name]
    size = [data["bounds"]["max"][a] - data["bounds"]["min"][a] for a in range(3)]
    assert h_lo <= size[1] <= h_hi, (name, size)
    assert w_lo <= size[0] <= w_hi and w_lo <= size[2] <= w_hi, (name, size)
    assert abs(size[1] - p["height_m"]) < 0.1 * p["height_m"] and abs(size[0] - p["width_m"]) < 0.1 * p["width_m"]
    leaves = slot_vertices(built["lods"][0], ("leaf",))
    lowest = min(v[1] for v in leaves)
    assert lowest >= p["bare_base_m"] and (name != "V13_tall" or lowest >= 0.9)
    assert data["bounds"]["min"][1] == 0.0


def test_lods_under_budget_and_halving(variant):
    name, p, built, data = variant
    tris = [lod["tris"] for lod in data["lods"]]
    assert len(tris) == 3 and tris[0] < 12000 and tris[1] <= tris[0] / 2 and tris[2] <= tris[1] / 2 and tris[2] == 6
    assert [lod["switch_distance_m"] for lod in data["lods"]] == [0.0, 7.5, pytest.approx(p["card_m"] * 467.5)]
    assert data["cull_distance_m"] == {"V13_low": 561.0, "V13_mid": 800.0, "V13_tall": 800.0}[name]
    assert 2 * shrub.card_count(p) == sum(1 for f in built["lods"][0].faces if f[3] == "leaf") * 2
    assert sum(1 for f in built["lods"][1].faces if f[3] == "leaf") == math.ceil(shrub.card_count(p) / 2)
    assert not any(f[3] == "twig" for f in built["lods"][1].faces) and all(f[3] == "leaf" for f in built["lods"][2].faces)


def axis_ends(capsule):
    """The end points of a capsule's axis (its chord), from its centre, height, radius and rotation."""
    d = audit._rotate(tuple(capsule["rotation"]), UP)
    half = capsule["height"] / 2 - capsule["radius"]
    return [[capsule["center"][a] - half * d[a] for a in range(3)], [capsule["center"][a] + half * d[a] for a in range(3)]]


def flat(points):
    return [c for p in points for c in p]


def test_metadata_carries_capsules_and_a_shell_of_pieces(variant, material_ids):
    name, p, built, data = variant
    assert meta.validate(data, material_ids) == []
    bark = [c for c in data["collision"] if c["material"] == "bark"]
    pieces = [c for c in data["collision"] if c["material"] == "twig"]
    assert len(bark) == p["stems"] * skeleton.SEGMENTS and all(c["shape"] == "capsule" for c in bark)
    assert all(0.005 <= c["radius"] <= p["stem_d_mm"] / 2000 and c["height"] >= 2 * c["radius"] for c in bark)
    assert 1 <= len(pieces) <= shell.MAX_PIECES and len(data["collision"]) == len(bark) + len(pieces)
    assert all(c["shape"] in ("sphere", "capsule") for c in pieces)
    assert data["gaps"] == [] and not any(c["landable"] for c in data["collision"])
    assert data["materials"] == [{"slot": "stem", "visual": "bark_shrub", "physics": "bark"},
                                 {"slot": "twig", "visual": "bark_shrub", "physics": "twig"},
                                 {"slot": "leaf", "visual": "leaf_shrub", "physics": "twig"}]


def test_a_hook_per_stem_from_base_to_tip(variant):
    name, p, built, data = variant
    hooks = data["snag"]
    assert len(hooks) == p["stems"] and all(h["kind"] == "hook" and h["radius_m"] == p["stem_d_mm"] / 2000 for h in hooks)
    for hook in hooks:
        assert len(hook["points"]) == skeleton.SEGMENTS + 1 and hook["points"][0][1] == 0.0
        assert hook["points"][-1][1] == max(pt[1] for pt in hook["points"])


def test_one_wind_box_over_the_clump(variant):
    name, p, built, data = variant
    (wind,) = data["wind_volumes"]
    lo, hi = built["lods"][0].bounds()
    assert wind["shape"] == "box" and wind["porosity"] == pytest.approx(0.35)
    assert wind["size"] == pytest.approx([hi[a] - lo[a] for a in range(3)], abs=1e-6)


def test_capsule_lies_on_its_segment():
    p0, p1 = (0.1, 0.0, 0.2), (0.4, 1.8, -0.1)
    capsule = meta.capsule(p0, p1, 0.007, "bark")
    direction = skeleton.normalize(skeleton.sub(p1, p0))
    assert audit._rotate(tuple(capsule["rotation"]), UP) == pytest.approx(direction)
    assert capsule["center"] == pytest.approx([0.25, 0.9, 0.05]) and capsule["radius"] == pytest.approx(0.007)
    assert capsule["height"] == pytest.approx(math.dist(p0, p1) + 0.014)
    assert flat(axis_ends(capsule)) == pytest.approx(flat([p0, p1]))
    assert meta.capsule(p0, p0, 0.2, "twig") == {"shape": "sphere", "center": list(p0), "radius": 0.2, "material": "twig",
                                                 "landable": False}
    assert meta.rotation_from_y(UP) == [0.0, 0.0, 0.0, 1.0]
    assert audit._rotate(tuple(meta.rotation_from_y((0.0, -1.0, 0.0))), UP) == pytest.approx((0.0, -1.0, 0.0))


def test_skeleton_radii_segments_and_branching(repo_root):
    import random
    p = load(repo_root, "V13_mid")
    envelope = shrub.Envelope(p["width_m"], p["bare_base_m"], p["height_m"], 0.06)
    stems, twigs = skeleton.build(random.Random(1), 10, 0.22, (1.5, 1.9), 0.009, 0.2, 0.6, envelope.reach)
    assert len(stems) == 10
    for stem in stems:
        radii = [r for _, r in stem.rings]
        assert len(stem.rings) == skeleton.RINGS + 1 and radii[0] == 0.009 and radii[-1] == skeleton.STEM_TIP_R
        assert radii == sorted(radii, reverse=True) and len(stem.segments) == skeleton.SEGMENTS
        assert stem.segments[0][0] == stem.rings[0][0] and stem.segments[-1][1] == stem.rings[-1][0]
        tip, base = stem.rings[-1][0], stem.rings[0][0]
        assert math.hypot(tip[0] - base[0], tip[2] - base[2]) == pytest.approx(0.2 * stem.length)
        assert tip[1] == pytest.approx(stem.length) and 1.5 <= stem.length <= 1.9
        assert math.hypot(base[0], base[2]) <= 0.22
    first = [t for t in twigs if t.level == 1]
    second = [t for t in twigs if t.level == 2]
    assert first and second and len(second) <= 2 * len(first)
    assert all(0.0015 <= t.r1 < t.r0 <= 0.004 for t in twigs)  # the sheet's 3-8 mm twigs
    assert all(t.p0[1] >= 0.6 for t in first)
    ends = {t.p1 for t in first}
    assert all(t.p0 in ends for t in second)
    assert all(envelope.inside(t.p1) or envelope.reach(t.p0, skeleton.normalize(skeleton.sub(t.p1, t.p0))) >= 0 for t in twigs)


def test_tubes_face_outward_with_metre_uvs():
    mesh = Mesh()
    rings = [((0.0, 0.0, 0.0), 0.009), ((0.0, 0.5, 0.0), 0.007), ((0.0, 1.0, 0.0), 0.005)]
    skeleton._tube(mesh, rings, 6, "stem", cap=True)
    assert len(mesh.faces) == 13 and mesh.triangles() == 28
    for indices, normal, uvs, slot in mesh.faces[:-1]:
        centroid = [sum(mesh.vertices[i][a] for i in indices) / 4 for a in range(3)]
        assert normal[0] * centroid[0] + normal[2] * centroid[2] > 0 and slot == "stem"
        assert max(v for _, v in uvs) - min(v for _, v in uvs) == pytest.approx(0.5)  # V runs up the stem in metres
    assert mesh.faces[-1][1] == (0.0, 1.0, 0.0) and len(mesh.faces[-1][0]) == 6


def test_cards_map_into_the_atlas_cells(variant):
    name, p, built, data = variant
    cells = cards.CELLS_UV
    assert all(0 <= u0 < u1 <= 1 and 0 <= v0 < v1 <= 1 for (u0, v0), (u1, v1) in cells) and len(cells) == 6
    for mesh in built["lods"][:2]:
        for indices, _, uvs, slot in mesh.faces:
            if slot != "leaf":
                continue
            (u0, v0), (u1, v1) = next(c for c in cells if c[0] == uvs[0])
            assert uvs == [(u0, v0), (u1, v0), (u1, v1), (u0, v1)]
            corners = [mesh.vertices[i] for i in indices]
            assert math.dist(corners[0], corners[3]) == pytest.approx(p["card_m"])
            assert math.dist(corners[0], corners[1]) == pytest.approx(p["card_m"] * (u1 - u0) / (v1 - v0))
    far = [cards.cell_uv(box) for box in cards.FAR_PX]
    assert [uvs for _, _, uvs, _ in built["lods"][2].faces] == [[(u0, v0), (u1, v0), (u1, v1), (u0, v1)] for (u0, v0), (u1, v1) in far]
    for ((x0, y0), (x1, y1)), cell in zip(cards.FAR_PX, (3, 5, 2)):  # each far core lies inside its sprig's cell
        (cx0, cy0), (cx1, cy1) = cards.CELLS_PX[cell]
        assert cx0 <= x0 < x1 <= cx1 and cy0 <= y0 < y1 <= cy1


def test_sprigs_leave_their_twig_at_the_fork_angle():
    """A card's stalk end sits on its twig and its axis leaves the twig at an angle inside the fork range, at
    azimuths all round it (the sprig look of the 1.3 tuning against H); fork 0 lays it along the twig."""
    import random
    twig = skeleton.Twig((0.0, 1.0, 0.0), (0.5, 1.5, 0.2), 0.0025, 0.0015, 2)
    along = skeleton.normalize(skeleton.sub(twig.p1, twig.p0))
    placed = cards.place(random.Random(7), [twig], 300, 0.25, fork_deg=(40.0, 70.0))
    sides = set()
    for card in placed:
        stalk = skeleton.scale(skeleton.add(card.corners[0], card.corners[1]), 0.5)
        rel = skeleton.sub(stalk, twig.p0)
        t = skeleton.dot(rel, along) / skeleton.norm(skeleton.sub(twig.p1, twig.p0))
        assert cards.MIN_T - 1e-9 <= t <= 1 + 1e-9
        assert skeleton.norm(skeleton.sub(rel, skeleton.scale(along, skeleton.dot(rel, along)))) < 1e-9
        axis = skeleton.normalize(skeleton.sub(card.corners[3], card.corners[0]))
        assert 40.0 - 1e-6 <= math.degrees(math.acos(skeleton.dot(axis, along))) <= 70.0 + 1e-6
        sides.add(skeleton.dot(skeleton.cross(along, axis), (0.0, 1.0, 0.0)) > 0)
    assert sides == {True, False}
    (straight,) = cards.place(random.Random(7), [twig], 1, 0.25)
    assert skeleton.dot(skeleton.normalize(skeleton.sub(straight.corners[3], straight.corners[0])), along) == pytest.approx(1.0)


@pytest.mark.parametrize("camera", [(-6.66, 4.37, -6.67), (-2.36, 1.5, -2.37), (4.0, 1.0, 0.5)])
def test_cards_face_the_viewer_outside_the_clump(repo_root, camera):
    """Most of the cards a camera outside the clump can see show it their front, so each card covers more of the
    clump for its two triangles (the leaf shader lights both faces right since task 1.4): the review sheet's 10 m
    and 1.5 m cameras and one low."""
    lod0 = build(repo_root, "V13_mid")[2]["lods"][0]
    near, front = 0, 0
    for indices, normal, _, slot in lod0.faces:
        if slot != "leaf":
            continue
        centre = [sum(lod0.vertices[i][k] for i in indices) / 4 for k in range(3)]
        to_camera = [camera[k] - centre[k] for k in range(3)]
        if centre[0] * to_camera[0] + centre[2] * to_camera[2] <= 0:
            continue  # on the far side of the clump from this camera
        near += 1
        front += sum(normal[k] * to_camera[k] for k in range(3)) > 0
    assert near > 100 and front / near >= 0.75, (front, near)  # 0.77-0.82 as tuned (sprigs off the twig); a random roll gives 0.5


def test_seed_determines_the_clump(repo_root, monkeypatch):
    name, p, built, data = build(repo_root, "V13_low")
    again = shrub.build(p)
    assert again["lods"][0].vertices == built["lods"][0].vertices
    assert meta.assemble("V13", name, "shrub", p, again, VISUALS) == data  # the shell and records too
    monkeypatch.setattr(shell, "fit", lambda *args: [])  # the clump's geometry is all this compares
    assert shrub.build(dict(p, seed=p["seed"] + 1))["lods"][0].vertices != built["lods"][0].vertices


@pytest.mark.parametrize("edit, expected", [
    (lambda p: p.update(bare_base_m=1.5, height_m=1.5), "'bare_base_m' = 1.5 must stay under 'height_m' = 1.5"),
    (lambda p: p.update(fork_angle_deg={"min": 60.0, "max": 45.0}), "'fork_angle_deg.min' = 60.0 must not exceed 'fork_angle_deg.max' = 45.0"),
    (lambda p: p.update(cards_per_m3=2000.0), "'cards_per_m3' = 2000.0 gives 7096 cards, 14192 triangles at LOD0, over the budget of 12000"),
    (lambda p: p.update(autumn=0.5), "'autumn' = 0.5 is not built yet (a per-variant leaf tint needs the importer to read one; the look-lock decides); keep 0.0"),
    (lambda p: p.update(spines=True), "'spines' = True is not built yet (no spine cell is pinned); keep False"),
    (lambda p: p.update(fruit_tufts=3), "'fruit_tufts' = 3 is not built yet (no berry cell is pinned); keep 0"),
])
def test_check_refuses(repo_root, edit, expected):
    edited = copy.deepcopy(load(repo_root, "V13_mid"))
    edit(edited)
    assert params.validate(shrub, edited) == [expected]


def test_low_variant_passes_the_collision_audit(repo_root):
    """The low variant's shell holds both ways: each piece within 0.1 m of a leaf or twig face, every twig vertex
    within 0.1 m of the pieces and every leaf vertex within 0.3 m (the build audits every variant)."""
    _, p, built, data = build(repo_root, "V13_low")
    problems, lines = audit.run(data, built["lods"][0], catalog.card_visuals(repo_root))
    assert problems == []
    pieces = [c for c in data["collision"] if c["material"] == "twig"]
    shell_lines = [line for line in lines if "twig shell" in line]
    assert sum(1 for line in shell_lines if "largest standoff" in line) == len(pieces)
    assert [line.split(":")[0] for line in shell_lines if line.startswith("audit slot")] == \
        ["audit slot leaf (cards, twig shell)", "audit slot twig (geometry, twig shell)"]
    assert sum(1 for line in lines if "capsule->mesh" in line) == p["stems"] * skeleton.SEGMENTS


FIRST_SPHERE = {"shape": "sphere", "center": [-0.007505, 1.257248, 0.022364], "radius": 1.106606, "material": "twig",
                "landable": False}  # V13_mid's shell as task 1.2 built it: one sphere bounding every leaf and twig


def test_the_first_bounding_sphere_fails_its_standoff(repo_root):
    """QA's R1: the one sphere stood up to 0.78 m off the leaves, round the bare stems; rule (a) fails it."""
    name, p, built, data = build(repo_root, "V13_mid")
    data = copy.deepcopy(data)
    data["collision"] = [c for c in data["collision"] if c["material"] == "bark"] + [FIRST_SPHERE]
    problems, _ = audit.run(data, built["lods"][0], catalog.card_visuals(repo_root))
    assert len(problems) == 1 and problems[0].startswith("collision[42] sphere (twig shell): largest standoff ")
    assert problems[0].endswith(" exceeds the tolerance 100 mm")
    assert 700 < float(problems[0].split("largest standoff ")[1].split(" mm")[0]) < 800
