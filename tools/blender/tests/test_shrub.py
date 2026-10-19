"""generators.shrub with lib.skeleton, lib.cards and lib.shell (p1-v13-shrub, "V13 belt understory shrub"): the
committed params conform and the defaults are V13_mid; the built sizes sit inside the sheet; LOD triangle counts
under budget and halving; every stem segment a bark capsule on its chord; the twig shell of twig spheres and
capsules, the committed 6 / 15 / 24 pieces reproduced by the cap by steps, which pass at its first step and meet one
2 m cube 6 / 15 / 24 at most (p1-v01-locust review-physics PR1, PR2); a snag record per capsule segment and per twig
(review-physics PR3, PR4), each naming its member's material (p1-v01-locust PR4); the wind box over the leafy mass
(PR5); no gap, nothing landable; the skeleton's radii, tubes and normals; card UVs inside the atlas cells, LOD1's
cards scaled to hold LOD0's silhouette and its twigs', the far cards' ellipses over the mean-coloured boxes and
covering what LOD0 covers (p1-v01-locust task 1.2b), each two faces back to back in the far slot, shaded with their
side's normal leaning up as the leaves' visible normal does (task 1.2j); sprig cards leaving their twig at the fork
angle; determinism per
seed; check's refusals; the low variant through the collision audit, and V13_mid's first, single sphere failing its
standoff."""
import copy
import json
import math

import pytest

from generators import shrub
from lib import audit, cards, catalog, meta, params, shell, skeleton
from lib.geometry import Mesh

VARIANTS = ("V13_low", "V13_mid", "V13_tall")
SHEET = {"V13_low": ((1.0, 1.5), (1.0, 2.0)), "V13_mid": ((1.5, 2.5), (1.5, 3.0)), "V13_tall": ((2.5, 3.5), (2.0, 3.0))}
COMMITTED_PIECES = {"V13_low": 6, "V13_mid": 15, "V13_tall": 24}  # the summer shells as p1-v13-shrub built them
VISUALS = {"stem": "bark_shrub", "twig": "bark_shrub", "leaf": "leaf_shrub", "far": "leaf_shrub_far"}
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
    assert len(tris) == 3 and tris[0] < 12000 and tris[1] <= tris[0] / 2 and tris[2] <= tris[1] / 2
    assert tris[2] == 2 * cards.CROSSED * (cards.FAR_SIDES - 2)  # two faces a card
    assert [lod["switch_distance_m"] for lod in data["lods"]] == [0.0, 7.5, pytest.approx(p["card_m"] * 467.5)]
    assert data["cull_distance_m"] == {"V13_low": 561.0, "V13_mid": 800.0, "V13_tall": 800.0}[name]
    assert 2 * shrub.card_count(p) == sum(1 for f in built["lods"][0].faces if f[3] == "leaf") * 2
    assert sum(1 for f in built["lods"][1].faces if f[3] == "leaf") == math.ceil(shrub.card_count(p) / 2)
    assert not any(f[3] == "twig" for f in built["lods"][1].faces) and all(f[3] == "far" for f in built["lods"][2].faces)


def axis_ends(capsule):
    """The end points of a capsule's axis (its chord), from its centre, height, radius and rotation."""
    d = audit._rotate(tuple(capsule["rotation"]), UP)
    half = capsule["height"] / 2 - capsule["radius"]
    return [[capsule["center"][a] - half * d[a] for a in range(3)], [capsule["center"][a] + half * d[a] for a in range(3)]]


def flat(points):
    return [c for p in points for c in p]


def twig_tubes(mesh):
    """(p0, p1, r0, r1) of every twig tube in build order, read off its LOD0 faces: TWIG_SIDES quads a tube, each
    [ring0 j, ring0 k, ring1 k, ring1 j], whose rings centre on the axis."""
    faces = [indices for indices, _, _, slot in mesh.faces if slot == "twig"]
    tubes = []
    for t in range(0, len(faces), skeleton.TWIG_SIDES):
        rings = [[mesh.vertices[q[0]] for q in faces[t:t + skeleton.TWIG_SIDES]],
                 [mesh.vertices[q[3]] for q in faces[t:t + skeleton.TWIG_SIDES]]]
        centres = [tuple(sum(v[a] for v in ring) / len(ring) for a in range(3)) for ring in rings]
        tubes.append((*centres, math.dist(centres[0], rings[0][0]), math.dist(centres[1], rings[1][0])))
    return tubes


def test_metadata_carries_capsules_and_a_shell_of_pieces(variant, material_ids):
    name, p, built, data = variant
    assert meta.validate(data, material_ids) == []
    bark = [c for c in data["collision"] if c["material"] == "bark"]
    pieces = [c for c in data["collision"] if c["material"] == "twig"]
    assert len(bark) == p["stems"] * skeleton.SEGMENTS and all(c["shape"] == "capsule" for c in bark)
    assert all(0.005 <= c["radius"] <= p["stem_d_mm"] / 2000 and c["height"] >= 2 * c["radius"] for c in bark)
    assert len(pieces) == COMMITTED_PIECES[name] and len(data["collision"]) == len(bark) + len(pieces)
    assert all(c["shape"] in ("sphere", "capsule") for c in pieces)
    assert data["gaps"] == [] and not any(c["landable"] for c in data["collision"])
    assert data["materials"] == [{"slot": "stem", "visual": "bark_shrub", "physics": "bark"},
                                 {"slot": "twig", "visual": "bark_shrub", "physics": "twig"},
                                 {"slot": "leaf", "visual": "leaf_shrub", "physics": "twig"},
                                 {"slot": "far", "visual": "leaf_shrub_far", "physics": "twig"}]


def test_stem_records_run_along_the_capsules_from_the_ground_to_the_tip(variant):
    """review-physics PR3: per stem an `edge` on each lower capsule's chord and a `hook` on the top one, ending at
    the tip, each at its capsule's radius."""
    name, p, built, data = variant
    bark = [c for c in data["collision"] if c["material"] == "bark"]
    tips = [face for face in built["lods"][0].faces if face[3] == "stem" and len(face[0]) == skeleton.STEM_SIDES[0]]
    assert len(tips) == p["stems"]
    for k, (indices, _, _, _) in enumerate(tips):
        chain = data["snag"][k * skeleton.SEGMENTS:(k + 1) * skeleton.SEGMENTS]
        capsules = bark[k * skeleton.SEGMENTS:(k + 1) * skeleton.SEGMENTS]
        assert [r["kind"] for r in chain] == ["edge"] * (skeleton.SEGMENTS - 1) + ["hook"]
        for record, capsule in zip(chain, capsules):
            assert flat(record["points"]) == pytest.approx(flat(axis_ends(capsule)), abs=1e-5)
            assert record["radius_m"] == capsule["radius"]
            assert record["material"] == capsule["material"] == "bark"  # p1-v01-locust PR4: its capsule's material
        assert chain[0]["points"][0][1] == 0.0
        assert all(a["points"][1] == b["points"][0] for a, b in zip(chain, chain[1:]))
        tip = [sum(built["lods"][0].vertices[i][a] for i in indices) / len(indices) for a in range(3)]
        assert chain[-1]["points"][1] == pytest.approx(tip, abs=1e-6)


def test_twig_records_follow_the_tubes(variant):
    """review-physics PR4: one record per twig on its tube's axis at its mean radius, an `edge` where other twigs
    leave its end, a `hook` where it ends free; 3 x stems + twigs records in all."""
    name, p, built, data = variant
    tubes = twig_tubes(built["lods"][0])
    stems = p["stems"] * skeleton.SEGMENTS
    assert len(data["snag"]) == stems + len(tubes) == {"V13_low": 227, "V13_mid": 197, "V13_tall": 309}[name]
    for record, (p0, p1, r0, r1) in zip(data["snag"][stems:], tubes):
        assert flat(record["points"]) == pytest.approx(flat([p0, p1]), abs=1e-5)
        assert record["radius_m"] == pytest.approx((r0 + r1) / 2, abs=1e-6)
        assert record["kind"] == ("edge" if any(math.dist(p1, tube[0]) < 1e-6 for tube in tubes) else "hook")
        assert record["material"] == "twig"  # p1-v01-locust PR4: the twig slot's physics, inside a piece or not
    assert {r["kind"] for r in data["snag"][stems:]} == {"edge", "hook"}


def test_the_committed_shells_come_back_at_the_first_step(variant, repo_root):
    """p1-v01-locust PR1 and PR2: the cap by steps passes the summer shells at 24, so the collision equals the
    committed metadata's, piece for piece; and rule (d) counts every piece of a clump in one 2 m cube."""
    name, p, built, data = variant
    path = repo_root / "unity" / "Assets" / "_Project" / "Art" / "Models" / "vegetation" / "V13" / f"{name}.assetmeta.json"
    committed = json.loads(path.read_text(encoding="utf-8"))
    assert data["collision"] == committed["collision"]
    pieces = [audit._Shape(c) for c in data["collision"] if c["material"] == "twig"]
    assert audit.neighbourhood([piece.box() for piece in pieces])[0] == len(pieces) == COMMITTED_PIECES[name]


def test_wind_box_spans_the_leafy_mass_from_its_lowest_leaf_or_twig(variant):
    """review-physics PR5: the box over the LOD0 leaf and twig vertices, its bottom at the lowest of them (0.82 m on
    V13_mid and 1.25 m on V13_tall as measured), not the ground."""
    name, p, built, data = variant
    (wind,) = data["wind_volumes"]
    crown = slot_vertices(built["lods"][0], ("leaf", "twig"))
    lo, hi = ([f(v[a] for v in crown) for a in range(3)] for f in (min, max))
    assert wind["shape"] == "box" and wind["rotation"] == [0.0, 0.0, 0.0, 1.0] and wind["porosity"] == pytest.approx(0.35)
    assert [wind["center"][a] - wind["size"][a] / 2 for a in range(3)] == pytest.approx(lo, abs=2e-6)
    assert [wind["center"][a] + wind["size"][a] / 2 for a in range(3)] == pytest.approx(hi, abs=2e-6)
    if name != "V13_low":
        assert lo[1] == pytest.approx({"V13_mid": 0.82, "V13_tall": 1.25}[name], abs=0.006)


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
    lengths = []
    for mesh in built["lods"][:2]:
        lengths.append(set())
        for indices, _, uvs, slot in mesh.faces:
            if slot != "leaf":
                continue
            (u0, v0), (u1, v1) = next(c for c in cells if c[0] == uvs[0])
            assert uvs == [(u0, v0), (u1, v0), (u1, v1), (u0, v1)]
            corners = [mesh.vertices[i] for i in indices]
            length = math.dist(corners[0], corners[3])
            assert math.dist(corners[0], corners[1]) == pytest.approx(length * (u1 - u0) / (v1 - v0))
            lengths[-1].add(round(length, 9))
    assert lengths[0] == {round(p["card_m"], 9)}
    assert len(lengths[1]) == 1 and lengths[1].pop() > p["card_m"] * math.sqrt(2) * 0.95  # every other card, grown
    far = [cards.cell_uv(box) for box in cards.FAR_PX]
    for k, (indices, normal, uvs, slot) in enumerate(built["lods"][2].faces):
        (u0, v0), (u1, v1) = far[k // 2 % len(far)]  # two faces a card
        assert slot == "far" and len(indices) == cards.FAR_SIDES and normal[1] == 0.0
        assert all(u0 <= u <= u1 and v0 <= v <= v1 for u, v in uvs)
    for ((x0, y0), (x1, y1)), cell in zip(cards.FAR_PX, (5, 1)):  # each far box lies inside its sprig's cell
        (cx0, cy0), (cx1, cy1) = cards.CELLS_PX[cell]
        assert cx0 <= x0 < x1 <= cx1 and cy0 <= y0 < y1 <= cy1


def test_lod1_holds_the_silhouette_of_lod0s_cards_and_twigs(variant):
    """LOD1 keeps every other card, on the two cells of the leaves' mean colour, scaled so that its cards' mean
    silhouette is all of LOD0's plus that of the twigs it drops (p1-v01-locust task 1.2b: LOD1 had 10-19 % fewer crown
    pixels than LOD0)."""
    name, p, built, data = variant
    lod0, lod1 = ([cards.Card([m.vertices[i] for i in f], n, uvs, cards.CELLS_UV.index((uvs[0], uvs[2])))
                   for f, n, uvs, s in m.faces if s == "leaf"] for m in built["lods"][:2])
    twig_m2 = sum(math.pi / 4 * math.dist(p0, p1) * (r0 + r1) for p0, p1, r0, r1 in twig_tubes(built["lods"][0]))
    assert twig_m2 > 0 and len(lod1) == math.ceil(len(lod0) / 2)
    assert {c.cell for c in lod1} == set(cards.MEAN_CELLS)  # the leaves' mean colour, whichever cells they drew
    assert cards.silhouette(lod1) == pytest.approx(cards.silhouette(lod0) + twig_m2)


def test_far_cards_cover_what_their_leaves_and_wood_cover():
    """One square card of cell 0 (0.394 of it opaque) 1 m wide facing a far card's plane, and a 1 m stem 20 mm thick
    beside it: the ellipse is centred on the card and FAR_OPAQUE of its area is the 0.394 m2 plus 0.02 m2 they cover
    (to the raster's 5 cm)."""
    (u0, v0), (u1, v1) = cards.CELLS_UV[0]
    card = cards.Card([(-0.5, 1.0, 0.0), (0.5, 1.0, 0.0), (0.5, 2.0, 0.0), (-0.5, 2.0, 0.0)], (0.0, 0.0, 1.0),
                      [(u0, v0), (u1, v0), (u1, v1), (u0, v1)], 0)
    stem = ((1.0, 0.0, 0.0), (1.0, 1.0, 0.0), 0.01, 0.01)
    (cu, cv), (a, b) = cards.outline([card], [stem], (0.0, 0.0), (1.0, 0.0, 0.0))
    assert (cu, cv) == pytest.approx((0.0, 1.5), abs=1e-9)
    assert a == pytest.approx(b)  # a square's moments make a circle
    assert cards.FAR_OPAQUE * math.pi * a * b == pytest.approx(0.394 + 0.02, rel=0.08)
    front, back = cards.crossed([card], count=1)
    assert len(front.corners) == cards.FAR_SIDES and back.corners == front.corners and back.uvs == front.uvs
    assert front.normal == pytest.approx((0.0, 0.0, -1.0)) and back.normal == pytest.approx((0.0, 0.0, 1.0))
    assert cards.crossed([]) is None


def test_far_cards_lean_as_the_leaves_seen_from_the_review_look_down(variant):
    """Task 1.2j: each far card is two faces back to back (for the one-sided far material, since the leaf shader
    turns a back face's normal round), every corner of a face shaded with its side's horizontal normal leaning up by
    `visible_lean`: the leaves' mean visible normal from 20 degrees above the horizon leans 11.4-11.9 degrees on the
    three variants, where a vertical plane's has no up share."""
    name, p, built, data = variant
    lod2 = built["lods"][2]
    lean = math.degrees(cards.visible_lean(leaf_cards(built["lods"][0])))
    assert lean == pytest.approx({"V13_low": 11.88, "V13_mid": 11.81, "V13_tall": 11.37}[name], abs=0.01)
    assert sorted(lod2.normals) == list(range(len(lod2.faces)))
    for f in range(0, len(lod2.faces), 2):
        (front, n_front, _, _), (back, n_back, _, _) = lod2.faces[f], lod2.faces[f + 1]
        assert [lod2.vertices[i] for i in front] == [lod2.vertices[i] for i in back]
        assert n_back == pytest.approx(tuple(-c for c in n_front))
        for k, side in ((f, n_front), (f + 1, n_back)):
            for n in lod2.normals[k]:
                assert math.degrees(math.asin(n[1])) == pytest.approx(lean, abs=1e-6)
                assert skeleton.dot(skeleton.normalize((n[0], 0.0, n[2])), side) == pytest.approx(1.0)
    assert data["lods"][2]["tris"] == 60


def leaf_cards(lod0):
    """LOD0's leaf faces as lib.cards.Card, their cells read back from the UVs."""
    return [cards.Card([lod0.vertices[i] for i in f], n, uvs, cards.CELLS_UV.index((uvs[0], uvs[2])))
            for f, n, uvs, s in lod0.faces if s == "leaf"]


def test_visible_lean_turns_each_card_to_the_viewer():
    """A level card faces up from every azimuth (90 degrees); a vertical one is turned to each viewer and has no up
    share (0); the two together, of equal opaque area, lean as their mean normal does from 20 degrees down."""
    (u0, v0), (u1, v1) = cards.CELLS_UV[0]
    uvs = [(u0, v0), (u1, v0), (u1, v1), (u0, v1)]
    level = cards.Card([(0, 1, 0), (1, 1, 0), (1, 1, 1), (0, 1, 1)], (0.0, 1.0, 0.0), uvs, 0)
    upright = cards.Card([(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0)], (0.0, 0.0, 1.0), uvs, 0)
    assert math.degrees(cards.visible_lean([level])) == pytest.approx(90.0)
    assert math.degrees(cards.visible_lean([upright])) == pytest.approx(0.0, abs=1e-9)
    down, want = math.radians(20.0), 0.0
    for k in range(8):
        az = 2 * math.pi * (k + 0.5) / 8
        towards_z = math.cos(az)
        # level: up x sin(down); upright: +z x (cos(down) cos(az)), whose part towards the viewer is cos(down) cos(az)^2
        want += math.atan2(math.sin(down), math.cos(down) * towards_z * towards_z) / 8
    assert cards.visible_lean([level, upright]) == pytest.approx(want)


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
    monkeypatch.setattr(shell, "fit", lambda *args: ([], 24, True))  # the clump's geometry is all this compares
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
    assert built["card_reach_m"] == p["card_m"] == 0.2 and audit.card_allowance(built["card_reach_m"]) == 0.3
    problems, lines = audit.run(data, built["lods"][0], card_visuals=catalog.card_visuals(repo_root),
                                card_reach_m=built["card_reach_m"])
    assert problems == []
    pieces = [c for c in data["collision"] if c["material"] == "twig"]
    shell_lines = [line for line in lines if "twig shell" in line]
    assert sum(1 for line in shell_lines if "largest standoff" in line) == len(pieces)
    assert [line.split(":")[0] for line in shell_lines if line.startswith("audit slot")] == \
        ["audit slot far (cards, twig shell)", "audit slot leaf (cards, twig shell)",
         "audit slot twig (geometry, twig shell)"]
    assert "over 0 vertices" in next(line for line in shell_lines if line.startswith("audit slot far"))  # LOD2's only
    assert sum(1 for line in lines if "capsule->mesh" in line) == p["stems"] * skeleton.SEGMENTS


FIRST_SPHERE = {"shape": "sphere", "center": [-0.007505, 1.257248, 0.022364], "radius": 1.106606, "material": "twig",
                "landable": False}  # V13_mid's shell as task 1.2 built it: one sphere bounding every leaf and twig


def test_the_first_bounding_sphere_fails_its_standoff(repo_root):
    """QA's R1: the one sphere stood up to 0.78 m off the leaves, round the bare stems; rule (a) fails it."""
    name, p, built, data = build(repo_root, "V13_mid")
    data = copy.deepcopy(data)
    data["collision"] = [c for c in data["collision"] if c["material"] == "bark"] + [FIRST_SPHERE]
    problems, _ = audit.run(data, built["lods"][0], card_visuals=catalog.card_visuals(repo_root),
                            card_reach_m=built["card_reach_m"])
    assert len(problems) == 1 and problems[0].startswith("collision[42] sphere (twig shell): largest standoff ")
    assert problems[0].endswith(" exceeds the tolerance 100 mm")
    assert 700 < float(problems[0].split("largest standoff ")[1].split(" mm")[0]) < 800
