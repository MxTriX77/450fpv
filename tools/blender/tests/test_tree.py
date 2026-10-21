"""generators.tree with lib.skeleton, lib.cards and lib.shell (p1-v01-locust, "V01 black locust belt tree"; design
decisions 1, 2, 4 and 6; review-physics PR4-PR8): the committed params conform and the defaults are V01_M, and the autumn variants are their summer variant at the autumn cover with pods (task 1.4); check's
refusals; sizes inside the sheet's rows; the capsule count printed, V01_M's between 150 and 300; LODs under budget
and halving, the far ones' leaves holding LOD0's silhouette and the wood's they drop (task 1.2b); every wood segment a bark capsule on its chord; twig rings at every kink at most 0.1 m apart with
polyline records through them; records naming their material by the sheet's rule; leaves on the last two orders and
pods on the share of twig tips; one crown wind box over the twig, leaf and pod mass above trunk columns, never
overlapping; shells fitted per main limb and a limb that fails split into its child subtrees; the gap search keeping
a 0.5 m opening and dropping a 0.4 m one, and shrinking an opening off a shell piece; the committed gaps clear of
every piece; determinism per seed. Builds here stub the shell fit (minutes per variant): the real shells are the
committed metadata's, which the build's audit held."""
import copy
import json
import math

import pytest

from generators import tree
from lib import audit, cards, geometry, meta, params, shell, skeleton

VARIANTS = ("V01_S", "V01_M", "V01_L")
ROWS = {"V01_S": ((4.0, 7.0), (0.05, 0.10), (2.0, 4.0)), "V01_M": ((8.0, 12.0), (0.12, 0.20), (3.0, 6.0)),
        "V01_L": ((12.0, 16.0), (0.20, 0.35), (5.0, 8.0))}  # §V01: height, trunk diameter and crown width per size
VISUALS = {"bark": "bark_locust", "twig": "bark_locust", "leaf": "leaf_locust", "leaf_autumn": "leaf_locust_autumn",
           "pod": "pod_locust"}
MODELS = ("unity", "Assets", "_Project", "Art", "Models", "vegetation", "V01")
QUICK = {}


def load(repo_root, variant):
    return json.loads((repo_root / "assets-src" / "vegetation" / f"{variant}.json").read_text(encoding="utf-8"))


def no_shell(*args, **kwargs):
    return [], shell.CAPS[0], True


def quick(repo_root, name, **edits):
    """(params, built, metadata) of a committed variant with `edits`, built once with the shell fit stubbed out."""
    key = (name, tuple(sorted(edits.items())))
    if key not in QUICK:
        p = dict(load(repo_root, name), **edits)
        fit, shell.fit = shell.fit, no_shell
        try:
            built = tree.build(p)
        finally:
            shell.fit = fit
        QUICK[key] = p, built, meta.assemble("V01", name, "tree", p, built, VISUALS)
    return QUICK[key]


@pytest.fixture(params=VARIANTS)
def variant(request, repo_root):
    return (request.param, *quick(repo_root, request.param))


def committed(repo_root, name):
    path = repo_root.joinpath(*MODELS, f"{name}.assetmeta.json")
    if not path.is_file():
        pytest.skip(f"{name} has not been built")
    return json.loads(path.read_text(encoding="utf-8"))


def test_params_conform_and_the_defaults_are_the_m_variant(repo_root):
    for name in VARIANTS:
        assert params.validate(tree, load(repo_root, name)) == [], name
    flat = params._flatten(load(repo_root, "V01_M"))
    assert set(flat) == set(tree.PARAMS)
    for key, spec in tree.PARAMS.items():
        assert spec["default"] == flat[key], key


# p1-v01-locust design decision 7 and task 1.4: the H autumn state, each its summer variant at the autumn cover (V01_S
# at the lowest its crown passes rule (d) at, review-physics §A.3) with the sheet's autumn pods on a fifth of the tips,
# so the skeleton stays as it is; task 1.5: a share of its leaves in the autumn slot, one crown mixing green and yellow.
AUTUMN = {"V01_S_autumn": ("V01_S", 0.5), "V01_M_autumn": ("V01_M", 0.3), "V01_L_autumn": ("V01_L", 0.3)}
AUTUMN_SHARE = 0.15


@pytest.mark.parametrize("name", sorted(AUTUMN))
def test_autumn_variants_are_their_summer_variant_at_the_autumn_cover_with_pods(repo_root, name):
    summer, cover = AUTUMN[name]
    p = load(repo_root, name)
    assert params.validate(tree, p) == []
    assert p == dict(load(repo_root, summer), leaf_cover=cover, fruit_share=0.2, autumn_share=AUTUMN_SHARE)
    _, autumn, _ = quick(repo_root, name)
    _, base, _ = quick(repo_root, summer)
    assert autumn["snag"] == base["snag"], "the skeleton is the summer variant's"
    assert {"pod", "leaf", "leaf_autumn"} <= set(autumn["lods"][0].slots())


@pytest.mark.parametrize("name", sorted(AUTUMN))
def test_the_autumn_share_moves_leaves_between_the_slots_and_nothing_else(repo_root, name):
    """Task 1.5: each leaf drawn into leaf_autumn at autumn_share from a stream of its own, so at any share the faces
    are the share-0 build's, face for face, only their slots differ, about the share of them autumn's at LOD0, and
    every LOD thins each slot on its own (LOD1 one card for every two, LOD2 one for every four)."""
    _, mixed, _ = quick(repo_root, name)
    _, plain, _ = quick(repo_root, name, autumn_share=0.0)
    assert "leaf_autumn" not in plain["lods"][0].slots()
    lod0 = [f[3] for f in mixed["lods"][0].faces]
    assert [f[:3] for f in mixed["lods"][0].faces] == [f[:3] for f in plain["lods"][0].faces]
    assert [s if s != "leaf_autumn" else "leaf" for s in lod0] == [f[3] for f in plain["lods"][0].faces]
    leaves = [s for s in lod0 if s in ("leaf", "leaf_autumn")]
    assert lod0.count("leaf_autumn") / len(leaves) == pytest.approx(AUTUMN_SHARE, abs=0.03)
    for lod, keep in ((1, 2), (2, 4)):
        counts = {s: sum(1 for f in mixed["lods"][lod].faces if f[3] == s) for s in ("leaf", "leaf_autumn")}
        assert counts == {s: math.ceil(lod0.count(s) / keep) for s in counts}
    assert mixed["collision"] == plain["collision"] and mixed["snag"] == plain["snag"] and mixed["gaps"] == plain["gaps"]
    assert [m["slot"] for m in mixed["materials"]] == ["bark", "twig", "leaf", "leaf_autumn", "pod"]


def test_thinning_keeps_one_card_of_each_facing_whatever_their_order():
    """Task 1.6, lib.cards.by_facing: 64 cards facing evenly round the horizontal, laid so that every fourth faces the
    first quadrant; one kept for every four keeps 4 in each quadrant, no two neighbours more than twice the even
    spacing apart, their mean normal within 0.05 of none (every fourth card in order keeps the first quadrant alone)."""
    order = [quadrant * 16 + k for k in range(16) for quadrant in range(4)]
    laid = [cards.Card([(0.0, 0.0, 0.0)] * 4, (math.cos(math.pi * j / 32), 0.0, math.sin(math.pi * j / 32)), [(0.0, 0.0)] * 4, 1)
            for j in order]
    assert {math.atan2(c.normal[2], c.normal[0]) // (math.pi / 2) for c in laid[::4]} == {0.0}
    kept = cards.by_facing(laid, 4)
    azimuths = sorted(math.degrees(math.atan2(c.normal[2], c.normal[0])) % 360 for c in kept)
    assert [sum(q * 90 <= a < q * 90 + 90 for a in azimuths) for q in range(4)] == [4, 4, 4, 4]
    assert max((azimuths[(i + 1) % 16] - a) % 360 for i, a in enumerate(azimuths)) <= 2 * 360 / 16
    assert all(abs(sum(c.normal[axis] for c in kept)) / len(kept) < 0.05 for axis in range(3))
    assert [laid.index(c) for c in kept] == sorted(laid.index(c) for c in kept), "kept in the cards' order"


@pytest.mark.parametrize("name", list(VARIANTS) + sorted(AUTUMN))
def test_thinned_leaves_face_as_their_slot_does(repo_root, name):
    """Task 1.6: of each leaf slot, LOD1's and LOD2's kept leaves weighted by their silhouette have the mean normal and
    the mean n n^T of all the slot's leaves at LOD0 within 0.1 (a tenth of a unit normal), so they face the light and the
    view as the slot does. Kept as every other and every fourth leaf in the twigs' order, V01_S_autumn's 13 yellow
    leaves of 52 lay 0.17 and 0.21 off, faced the review's sun 86 % more than the 52, and its LOD2 switch read +6.52 %
    by day."""
    _, built, _ = quick(repo_root, name)

    def moments(mesh, slot):
        faces = [(f, n, uvs) for f, n, uvs, s in mesh.faces if s == slot]
        weights = [cards.CELL_OPAQUE[cards.CELLS_UV.index((uvs[0], uvs[2]))]
                   * skeleton.norm(skeleton.cross(skeleton.sub(mesh.vertices[f[1]], mesh.vertices[f[0]]),
                                                  skeleton.sub(mesh.vertices[f[2]], mesh.vertices[f[1]]))) for f, _, uvs in faces]
        total = sum(weights)
        return [sum(w * n[a] for w, (_, n, _) in zip(weights, faces)) / total for a in range(3)] + \
               [sum(w * n[a] * n[b] for w, (_, n, _) in zip(weights, faces)) / total for a in range(3) for b in range(a, 3)]

    for slot in ("leaf", "leaf_autumn"):
        if slot not in built["lods"][0].slots():
            continue
        all_leaves = moments(built["lods"][0], slot)
        for lod in (1, 2):
            assert max(abs(a - b) for a, b in zip(moments(built["lods"][lod], slot), all_leaves)) < 0.1, (slot, lod)


@pytest.mark.parametrize("edit, expected", [
    (lambda p: p.update(first_fork_m=6.0, height_m=6.0, crown_width_m=3.0), "'first_fork_m' = 6.0 must stay under 'height_m' = 6.0"),
    (lambda p: p.update(crown_width_m=7.0), "'crown_width_m' = 7.0 lies outside the sheet's 3-6 m for a tree 10 m tall"),
    (lambda p: p.update(height_m=7.5), "'height_m' = 7.5 lies in no size row of the sheet (4-7 m, 8-12 m, 12-16 m)"),
    (lambda p: p.update(crown_shape="dome"), "'crown_shape' = 'dome' is not built; the generator builds scraggly"),
    (lambda p: p.update(fork_angle_deg={"min": 40.0, "max": 25.0}),
     "'fork_angle_deg.min' = 40.0 must not exceed 'fork_angle_deg.max' = 25.0"),
    (lambda p: p.update(spines=True), "'spines' = True is not built yet (no thorn cell is pinned; thorns on suckers are V16's); keep False"),
    (lambda p: p["lod"].update(impostor_switch_m=116.9),
     "'lod.impostor_switch_m' = 116.9 must lie past 'lod.lod2_switch_m' = 116.9 and before 'lod.cull_m' = 2500.0"),
])
def test_check_refuses(repo_root, edit, expected):
    edited = copy.deepcopy(load(repo_root, "V01_M"))
    edit(edited)
    assert params.validate(tree, edited) == [expected]


def test_sizes_inside_the_sheet(variant):
    name, p, built, data = variant
    (h_lo, h_hi), (d_lo, d_hi), (w_lo, w_hi) = ROWS[name]
    size = [data["bounds"]["max"][a] - data["bounds"]["min"][a] for a in range(3)]
    height = data["bounds"]["max"][1]
    assert h_lo <= height <= h_hi and abs(height - p["height_m"]) < 0.1 * p["height_m"], (name, size)
    assert w_lo <= max(size[0], size[2]) <= w_hi and abs(max(size[0], size[2]) - p["crown_width_m"]) < 0.25 * p["crown_width_m"]
    trunks = [m for m in built["members"] if m.order == 0]
    assert len(trunks) == p["stems"]
    for trunk in trunks:
        (y0, r0), (y1, r1) = next(((a[1], ra), (b[1], rb)) for a, b, ra, rb in trunk.segments() if a[1] <= skeleton.DBH_AT_M <= b[1])
        at_dbh = r0 + (r1 - r0) * (skeleton.DBH_AT_M - y0) / (y1 - y0)
        assert 2 * at_dbh == pytest.approx(p["dbh_m"]) and d_lo <= p["dbh_m"] <= d_hi
        assert trunk.points[-1][1] == pytest.approx(p["first_fork_m"]) and 2.0 <= p["first_fork_m"] <= 5.0
    assert sum(m.order == 1 for m in built["members"]) == p["main_limbs"] * p["stems"]


def test_capsule_count_is_printed_and_the_m_tree_lies_between_150_and_300(variant):
    """The sheet's open point: the generator counts every segment of 10 mm or more as a capsule and prints it."""
    name, p, built, data = variant
    bark = [c for c in data["collision"] if c["material"] == "bark"]
    (line,) = [line for line in built["report"] if line.startswith("capsules: ")]
    assert line.startswith(f"capsules: {len(bark)} bark capsules of 10 mm or more (trunk ")
    assert len(bark) == sum(len(m.segments()) for m in built["members"] if not m.twig)
    if name == "V01_M":
        assert 150 <= len(bark) <= 300


def test_lods_under_budget_and_halving(variant):
    name, p, built, data = variant
    tris = [lod["tris"] for lod in data["lods"]]
    assert len(tris) == 3 and tris[1] <= tris[0] / 2 and tris[2] <= tris[1] / 2
    if name == "V01_L":
        assert tris[0] < 40000
    lod = p["lod"]
    assert [l["switch_distance_m"] for l in data["lods"]] == [0.0, lod["lod1_switch_m"], lod["lod2_switch_m"]]
    assert data["cull_distance_m"] == lod["cull_m"] == min(2500.0, p["height_m"] * 467.5)
    assert lod["lod1_switch_m"] == pytest.approx(0.010 * 935, abs=0.01) and lod["lod2_switch_m"] == pytest.approx(p["leaf_m"] * 467.5, abs=0.1)
    slots = [{f[3] for f in mesh.faces} for mesh in built["lods"]]
    assert slots[0] == slots[1] == slots[2] == {"bark", "twig", "leaf"}
    assert all(mesh.smooth == {"bark", "twig"} for mesh in built["lods"])  # the tubes shade round (task 1.2g)
    leaves = [sum(1 for f in mesh.faces if f[3] == "leaf") for mesh in built["lods"]]
    assert leaves[1] == math.ceil(leaves[0] / 2) and leaves[2] == math.ceil(leaves[0] / 4)


def test_far_lods_hold_the_leaves_and_the_wood(variant, monkeypatch):
    """p1-v01-locust task 1.2b (LOD2 had 43-52 % of LOD0's crown pixels): LOD1 and LOD2 keep every other and every
    fourth leaf, on the two cells of the leaves' mean colour, scaled so that their leaves' mean silhouette is all of
    LOD0's; and (task 1.2g: leaf cards standing for the wood a far LOD dropped took V01's LOD2 10-14 % over LOD0)
    their wood's tubes hold LOD0's tubes' mean silhouette, a convex tube's surface area over 4, within 2 %: fewer
    rings on grown radii (task 1.3), LOD1's twigs on their chords, every other one at LOD2 standing for two. The
    domes are left out: LOD0's bases lie buried, and a far LOD drops them (test_far_tubes_keep_lod0s_sides_over_fewer_rings)."""
    name, p, built, data = variant
    lods = [[cards.Card([m.vertices[i] for i in f], n, uvs, cards.CELLS_UV.index((uvs[0], uvs[2])))
             for f, n, uvs, s in m.faces if s == "leaf"] for m in built["lods"]]
    assert {c.cell for c in lods[1] + lods[2]} == set(cards.MEAN_CELLS) and len({c.cell for c in lods[0]}) == 6
    for lod in (1, 2):
        assert cards.silhouette(lods[lod]) == pytest.approx(cards.silhouette(lods[0]))
    monkeypatch.setattr(skeleton, "_dome", lambda *args: None)
    tubes = []
    for lod in range(3):
        mesh = geometry.Mesh()
        skeleton.tree_tubes(mesh, built["members"], lod, lambda m: "twig" if m.twig else "bark")
        tubes.append(mesh)

    def wood(mesh, slot):
        total = 0.0
        for f, _, _, s in mesh.faces:
            if s == slot:
                c = [mesh.vertices[i] for i in f]
                total += sum(skeleton.norm(skeleton.cross(skeleton.sub(c[k], c[0]), skeleton.sub(c[k + 1], c[0])))
                             for k in range(1, len(c) - 1)) / 2
        return total / 4

    for slot in ("bark", "twig"):
        for lod in (1, 2):
            assert wood(tubes[lod], slot) == pytest.approx(wood(tubes[0], slot), rel=0.02), (slot, lod)


def test_far_tubes_keep_lod0s_sides_over_fewer_rings(variant, monkeypatch):
    """p1-v01-locust task 1.3: a far LOD's tube of fewer sides than LOD0's, shaded smooth, showed each face's normals
    over 360/n degrees, so more of it faced the camera at a grazing angle, where URP's Fresnel term is whole (V01_M's
    LOD2 wood specular 70 % over LOD0's at 50 and 190 m, measured), and LOD0's spread of normals on 3 and 4 sides left
    the few large trunk and limbs lit by how their faces turned (its wood 16 % dark by day): every LOD keeps LOD0's
    sides (trunk 12, limb 8, branch 6, twig 3) over every RING_STEP-th ring and the last, a branch at LOD2 its chord,
    the radii grown by the member's length over the kept polyline, smooth, with no normals of their own, each
    starting its frame where LOD0's starts (from its own first chord the frame of V01_M's trunk turned a quarter turn
    from LOD0's, and its bark showed other texels: the far LODs' wood 7-8 % dark unlit, measured); LOD1 domes only the
    trunk's and the limbs' free ends, LOD2 nothing."""
    name, p, built, data = variant
    members = built["members"]
    calls = {lod: [] for lod in range(3)}
    tube, dome = skeleton._tube, skeleton._dome

    def record(mesh, rings, sides, slot, cap, tangents=None, uv0=(0.0, 0.0), first=None):
        calls[lod].append((uv0, sides, rings, first))
        return tube(mesh, rings, sides, slot, cap, tangents, uv0, first)

    def record_dome(mesh, ring, outward, sides, slot, u):
        calls[lod].append(("dome", sides, ring, None))
        return dome(mesh, ring, outward, sides, slot, u)

    monkeypatch.setattr(skeleton, "_tube", record)
    monkeypatch.setattr(skeleton, "_dome", record_dome)
    for lod in range(3):
        mesh = geometry.Mesh()
        mesh.smooth = {"bark", "twig"}
        skeleton.tree_tubes(mesh, members, lod, lambda m: "twig" if m.twig else "bark")
        assert not mesh.normals
    by_patch = {skeleton.bark_patch(i): m for i, m in enumerate(members)}
    for lod in (1, 2):
        domes = [c for c in calls[lod] if c[0] == "dome"]
        assert len(domes) == (sum(1 for m in members if not m.twig and m.order < 2) if lod == 1 else 0)
        for uv0, sides, rings, first in (c for c in calls[lod] if c[0] != "dome"):
            m = by_patch[uv0]
            kind = 3 if m.twig else min(m.order, 2)
            assert sides == skeleton.TREE_SIDES[kind] == (12, 8, 6, 3)[kind]
            start = skeleton.normalize(skeleton.sub(m.points[1], m.points[0]))  # LOD0's frame, so LOD0's texels
            assert first == skeleton.cross(start, skeleton._helper(start))
            if m.twig:
                assert len(rings) == 2
                continue
            step = skeleton.RING_STEP[kind][lod] or len(m.points) - 1
            assert [r[0] for r in rings] == m.points[::step] + ([m.points[-1]] if (len(m.points) - 1) % step else [])
            kept = sum(skeleton.norm(skeleton.sub(b[0], a[0])) for a, b in zip(rings, rings[1:]))
            assert rings[0][1] == pytest.approx(m.radii[0] * m.length() / kept)
    assert skeleton.RING_STEP == ((1, 2, 8), (1, 2, 8), (1, 2, None))


def test_each_member_takes_its_own_bark_patch_at_every_lod(variant, monkeypatch):
    """p1-v01-locust task 1.2l: every member's tube starts its UVs at its own patch of the bark tile, the R2
    sequence by its index, the same at every LOD, so between them the members sample the whole tile (as the far mips
    do), not its first columns; the shrub's tubes still start at the corner."""
    name, p, built, data = variant
    members = built["members"]
    calls = {lod: [] for lod in range(3)}
    tube = skeleton._tube

    def record(mesh, rings, sides, slot, cap, tangents=None, uv0=(0.0, 0.0), first=None):
        calls[lod].append((rings[0][0], uv0))
        return tube(mesh, rings, sides, slot, cap, tangents, uv0, first)

    monkeypatch.setattr(skeleton, "_tube", record)
    for lod in range(3):
        skeleton.tree_tubes(geometry.Mesh(), members, lod, lambda m: "twig" if m.twig else "bark")
    patches = [skeleton.bark_patch(i) for i in range(len(members))]
    assert [uv for _, uv in calls[0]] == patches
    starts = {}
    for m, uv in zip(members, patches):
        starts.setdefault(m.points[0], set()).add(uv)
    for lod in (1, 2):  # a far LOD drops twigs, never moves a member's patch
        assert calls[lod] and all(uv in starts[point] for point, uv in calls[lod])
    assert len(set(patches)) == len(members) and all(0.0 <= c < 1.0 for uv in patches for c in uv)
    for axis in (0, 1):  # spread over the tile: a quarter of the members in each half of each axis at least
        assert 0.25 < sum(uv[axis] < 0.5 for uv in patches) / len(patches) < 0.75
    assert skeleton.bark_patch(0) == pytest.approx((0.2548776662466927, 0.0698402909980532))


def axis_ends(capsule):
    d = audit._rotate(tuple(capsule["rotation"]), (0.0, 1.0, 0.0))
    half = capsule["height"] / 2 - capsule["radius"]
    return [[capsule["center"][a] - half * d[a] for a in range(3)], [capsule["center"][a] + half * d[a] for a in range(3)]]


def flat(points):
    return [c for p in points for c in p]


def test_every_wood_segment_is_a_bark_capsule_on_its_chord(variant, material_ids):
    name, p, built, data = variant
    assert meta.validate(data, material_ids) == []
    bark = [c for c in data["collision"] if c["material"] == "bark"]
    segments = [s for m in built["members"] if not m.twig for s in m.segments()]
    assert len(bark) == len(segments) and all(c["shape"] == "capsule" and not c["landable"] for c in bark)
    for capsule, (p0, p1, r0, r1) in zip(bark, segments):
        assert flat(axis_ends(capsule)) == pytest.approx(flat([p0, p1]), abs=2e-6)
        assert capsule["radius"] == pytest.approx((r0 + r1) / 2, abs=1e-6)
    assert all(2 * m.radii[0] >= skeleton.TWIG_D_M for m in built["members"] if not m.twig)


def test_twig_rings_at_every_kink_and_polyline_records(variant):
    """PR6: every twig is a 3-sided tube with a ring at each kink, consecutive ring centres at most 0.1 m apart, and
    its record is a polyline through the ring centres, which the LOD0 tube's rings centre on."""
    name, p, built, data = variant
    twigs = [m for m in built["members"] if m.twig]
    records = [r for r in data["snag"] if r["material"] == "twig"]
    assert twigs and len(records) == len(twigs)
    faces = [indices for indices, _, _, slot in built["lods"][0].faces if slot == "twig"]
    vertices = built["lods"][0].vertices
    at = 0
    for twig, record in zip(twigs, records):
        assert 2 * twig.radii[0] < skeleton.TWIG_D_M
        steps = [math.dist(a, b) for a, b in zip(twig.points, twig.points[1:])]
        assert max(steps) <= skeleton.TWIG_STEP_M + 1e-9
        bands = faces[at:at + skeleton.TWIG_SIDES * len(steps)]
        at += len(bands)
        rings = [[vertices[q[0]] for q in bands[k * 3:k * 3 + 3]] for k in range(len(steps))] + \
                [[vertices[q[3]] for q in bands[-3:]]]
        centres = [[sum(v[a] for v in ring) / 3 for a in range(3)] for ring in rings]
        assert flat(record["points"]) == pytest.approx(flat(centres), abs=2e-6)
        assert record["kind"] == "hook" and record["radius_m"] == pytest.approx((twig.radii[0] + twig.radii[-1]) / 2, abs=1e-6)
    assert at == len(faces)


def test_records_name_their_material_by_the_sheet_rule(variant):
    """PR4 and the sheet: a wood segment under 30 mm an edge, every wood member's top segment a hook to its free
    tip, each on its capsule's axis at its radius in bark; every twig one hook in twig."""
    name, p, built, data = variant
    expected = []
    for m in built["members"]:
        if m.twig:
            expected.append(("hook", "twig", len(m.points)))
            continue
        segments = m.segments()
        for k, (p0, p1, r0, r1) in enumerate(segments):
            if k == len(segments) - 1:
                expected.append(("hook", "bark", 2))
            elif r0 + r1 < tree.SNAG_EDGE_M:
                expected.append(("edge", "bark", 2))
    assert [(r["kind"], r["material"], len(r["points"])) for r in data["snag"]] == expected
    bark = [(flat(axis_ends(c)), c["radius"]) for c in data["collision"] if c["material"] == "bark"]
    for record in data["snag"]:
        if record["material"] == "bark":  # on its capsule's axis, at its radius
            points = flat(record["points"])
            assert any(radius == record["radius_m"] and max(abs(a - b) for a, b in zip(ends, points)) < 2e-6 for ends, radius in bark)
    assert {r["material"] for r in data["snag"]} == {"bark", "twig"}


def test_leaves_on_the_last_two_orders(variant):
    name, p, built, data = variant
    members = built["members"]
    leaf_faces = [f for f in built["lods"][0].faces if f[3] == "leaf"]
    assert leaf_faces
    vertices = built["lods"][0].vertices
    leafy = [m for m in members if m.order >= p["orders"] - 1]
    for indices, _, uvs, _ in leaf_faces[:200]:
        corners = [vertices[i] for i in indices]
        stalk = [(corners[0][a] + corners[1][a]) / 2 for a in range(3)]
        tip = [(corners[2][a] + corners[3][a]) / 2 for a in range(3)]
        assert math.dist(stalk, tip) == pytest.approx(p["leaf_m"])
        assert uvs[0] in [cell[0] for cell in cards.CELLS_UV]  # a whole sprig cell: the pinnate leaf
        assert min(min(math.sqrt(shell._segment_distance2(stalk, a, b)) for a, b, _, _ in m.segments()) for m in leafy) < 1e-6


def test_card_reach_is_the_far_corner_of_the_widest_card(variant):
    """review-physics §A.3 and §A.6 item 5: rule (c)'s card reach is a leaf card's farthest corner from its petiole,
    leaf_m * sqrt(1 + (aspect / 2)^2) at the widest cell, 0.261 m at the 0.25 m leaf (an allowance of 0.361 m), and
    the built cards reach exactly that far, no farther."""
    name, p, built, data = variant
    vertices = built["lods"][0].vertices
    reach = 0.0
    for indices, _, _, slot in built["lods"][0].faces:
        if slot == "leaf":
            c = [vertices[i] for i in indices]
            petiole = [(c[0][a] + c[1][a]) / 2 for a in range(3)]
            reach = max(reach, *(math.dist(petiole, corner) for corner in c))
    assert built["card_reach_m"] == cards.card_reach(p["leaf_m"]) == pytest.approx(reach, abs=1e-9)
    assert cards.card_reach(0.25) == pytest.approx(0.261026, abs=1e-6) and audit.card_allowance(cards.card_reach(0.25)) == 0.361026


def test_pods_hang_from_the_share_of_twig_tips(repo_root):
    p, built, data = quick(repo_root, "V01_M", fruit_share=0.2)
    members = built["members"]
    last = [m for m in members if m.order == p["orders"]]
    pods = [f for f in built["lods"][0].faces if f[3] == "pod"]
    strips = len(pods) // 2
    assert 0.1 * len(last) <= strips <= 0.3 * len(last) and len(pods) == 2 * strips
    tips = {tuple(round(c, 6) for c in m.points[-1]) for m in last}
    vertices = built["lods"][0].vertices
    for indices, normal, _, _ in pods:
        corners = [vertices[i] for i in indices]
        top = [(corners[2][a] + corners[3][a]) / 2 for a in range(3)]
        assert tuple(round(c, 6) for c in top) in tips and abs(normal[1]) < 1e-9
        assert max(c[1] for c in corners) - min(c[1] for c in corners) == pytest.approx(tree.POD_M)
    assert {"slot": "pod", "visual": "pod_locust", "physics": "twig"} in data["materials"]
    assert sum(1 for f in built["lods"][1].faces if f[3] == "pod") == 2 * math.ceil(strips / 2)
    assert not any(f[3] == "pod" for f in built["lods"][2].faces)


def boxes_overlap(a, b):
    """Two boxes (metadata dicts) overlap with some volume: the separating axis test over their 15 axes."""
    def frame(box):
        q = tuple(box["rotation"])
        return box["center"], [audit._rotate(q, e) for e in ((1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0))], [s / 2 for s in box["size"]]
    (ca, ua, ha), (cb, ub, hb) = frame(a), frame(b)
    t = [cb[k] - ca[k] for k in range(3)]
    axes = ua + ub + [audit._cross(x, y) for x in ua for y in ub]
    for axis in axes:
        if audit._dot(axis, axis) < 1e-12:
            continue
        ra = sum(h * abs(audit._dot(u, axis)) for u, h in zip(ua, ha))
        rb = sum(h * abs(audit._dot(u, axis)) for u, h in zip(ub, hb))
        if abs(audit._dot(t, axis)) >= ra + rb - 1e-9:
            return False
    return True


def test_wind_volumes_never_overlap_and_the_crown_box_starts_at_the_lowest_twig_leaf_or_pod(variant):
    """PR8: one crown box over the LOD0 twig, leaf and pod vertices at the state's porosity, and a solid column of
    width dbh per trunk from the ground to its base."""
    name, p, built, data = variant
    crown, *columns = data["wind_volumes"]
    lod0 = built["lods"][0]
    mass = [lod0.vertices[i] for indices, _, _, slot in lod0.faces if slot in ("twig", "leaf", "pod") for i in indices]
    lo = [min(v[a] for v in mass) for a in range(3)]
    assert crown["center"][1] - crown["size"][1] / 2 == pytest.approx(lo[1], abs=2e-6)
    assert crown["rotation"] == [0.0, 0.0, 0.0, 1.0] and crown["porosity"] == pytest.approx(tree.porosity(p["leaf_cover"]))
    assert len(columns) == p["stems"]
    for column in columns:
        assert column["porosity"] == 0.0 and column["size"][0] == column["size"][2] == p["dbh_m"]
        assert column["center"][1] - column["size"][1] / 2 < 0.01  # from the ground
    volumes = data["wind_volumes"]
    assert not any(boxes_overlap(a, b) for i, a in enumerate(volumes) for b in volumes[i + 1:])


@pytest.mark.parametrize("cover, porosity", [(0.0, 0.9), (0.3, 0.825), (0.6, 0.75), (0.9, 0.55), (1.0, 0.55)])
def test_porosity_by_state(cover, porosity):
    """The sheet's points (bare 0.9, part-bare 0.75, summer 0.55) and review-physics N-g's 0.825 at autumn's 0.3."""
    assert tree.porosity(cover) == pytest.approx(porosity)


def test_shells_are_fitted_per_main_limb_and_a_failing_limb_split_into_its_child_subtrees(repo_root, monkeypatch):
    """PR5: one fit per main limb over its subtree's twig, leaf and pod faces; a limb whose 64 pieces do not pass
    is fitted again as its child subtrees; every fit's pieces join the collision as twig."""
    calls = []

    def fit(mesh, geometry_slots, card_slots, allowance, caps=shell.CAPS):
        calls.append((sum(1 for f in mesh.faces if f[3] == "twig"), sum(1 for f in mesh.faces if f[3] == "leaf"), allowance))
        return [((0.0, 5.0 + len(calls), 0.0), (0.0, 5.0 + len(calls), 0.0), 0.1)], caps[-1], len(calls) != 1

    monkeypatch.setattr(shell, "fit", fit)
    p = load(repo_root, "V01_M")
    built = tree.build(p)
    members = built["members"]
    below = tree._subtrees(members)
    limbs = [i for i, m in enumerate(members) if m.order == 1]

    def twig_faces(indices):
        return sum(skeleton.TWIG_SIDES * (len(members[i].points) - 1) for i in indices if members[i].twig)

    first = limbs[0]
    groups = [below[first]] + [below[c] for c in members[first].children] + [[first]] + [below[l] for l in limbs[1:]]
    fitted = [g for g in groups if twig_faces(g) or any(members[i].order >= p["orders"] - 1 for i in g)]
    assert [c[0] for c in calls] == [twig_faces(g) for g in fitted]
    assert calls[0][1] == sum(c[1] for c in calls[1:1 + len(members[first].children)])  # the children hold its leaves
    assert all(c[2] == audit.card_allowance(cards.card_reach(p["leaf_m"])) == 0.361026 for c in calls)
    pieces = [c for c in built["collision"] if c["material"] == "twig"]
    assert len(pieces) == len(calls) - 1 and any("split into its child subtrees" in line for line in built["report"])


def ground_pair(spacing, radius=0.04, height=2.5):
    """Two trunks standing `spacing` apart, axis to axis, with their capsules."""
    members = [skeleton.Member([(x, 0.0, 0.0), (x, height, 0.0)], [radius, radius], 0) for x in (-spacing / 2, spacing / 2)]
    return members, [meta.capsule(m.points[0], m.points[1], radius, "bark") for m in members]


@pytest.mark.parametrize("clear_m, kept", [(0.51, True), (0.4, False)])
def test_an_opening_of_half_a_metre_is_kept_and_one_of_0_4_m_dropped(clear_m, kept):
    """The gap search writes the opening between two trunks of a clump when its largest inscribed circle is 0.5 m
    or more: its sides stand GAP_MARGIN_M off the trunks' surfaces."""
    members, collision = ground_pair(clear_m + 2 * 0.04 + 2 * tree.GAP_MARGIN_M)
    report = []
    gaps = tree._gaps(members, collision, [], report)
    if not kept:
        assert gaps == []
        return
    (gap,) = gaps
    assert gap["narrowest_m"] == pytest.approx(clear_m, abs=0.005)
    assert 2 * audit._inscribed_radius(audit._flatten_polygon(gap["polygon"], gap["normal"])) == pytest.approx(gap["narrowest_m"], abs=0.005)
    assert meta._gap_problems(gap) == [] and report[0].startswith("gap g0 (stems ") and "narrowest 0.510 m" in report[0]


def lattice(gap, spacing=0.02, inside=0.01):
    """Points of a lattice in the gap's plane, `inside` or more within its polygon (PR7's test)."""
    u, v = audit._plane_basis(gap["normal"])
    polygon = audit._flatten_polygon(gap["polygon"], gap["normal"])
    offset = audit._dot(gap["polygon"][0], gap["normal"])
    xs, ys = [p[0] for p in polygon], [p[1] for p in polygon]
    out = []
    for i in range(int((max(xs) - min(xs)) / spacing) + 1):
        for j in range(int((max(ys) - min(ys)) / spacing) + 1):
            q = (min(xs) + i * spacing, min(ys) + j * spacing)
            if audit._signed_distance2(q, polygon) >= inside:
                out.append(tuple(q[0] * u[a] + q[1] * v[a] + offset * gap["normal"][a] for a in range(3)))
    return out


def test_a_shell_piece_in_the_opening_closes_it_where_it_begins():
    """PR7: the opening search treats shell pieces as solid, so the chord stops short of a piece standing in it;
    no point 1 cm inside the written polygon lies in the piece."""
    members, collision = ground_pair(0.63)
    piece = meta.capsule((0.0, 2.0, 0.0), (0.0, 2.0, 0.0), 0.1, "twig")
    (gap,) = tree._gaps(members, collision + [piece], [], [])
    assert max(p[1] for p in gap["polygon"]) < 1.9
    points = lattice(gap)
    assert points and all(audit._Shape(piece).sdf(q) > 0 for q in points)


def test_the_committed_gaps_are_clear_of_every_piece_and_capsule(repo_root):
    """PR7 on the built trees: no shell piece or capsule meets a written polygon's interior (points 1 cm inside its
    boundary on a 2 cm lattice lie outside every one), and each narrowest_m is its inscribed circle within 5 mm; the
    autumn variants' too, whose sparser crowns take other pieces."""
    seen = 0
    for name in VARIANTS + tuple(sorted(AUTUMN)):
        data = committed(repo_root, name)
        shapes = [audit._Shape(c) for c in data["collision"]]
        for gap in data["gaps"]:
            seen += 1
            assert gap["narrowest_m"] >= tree.GAP_MIN_M
            diameter = 2 * audit._inscribed_radius(audit._flatten_polygon(gap["polygon"], gap["normal"]))
            assert diameter == pytest.approx(gap["narrowest_m"], abs=0.005)
            points = lattice(gap)
            assert points and all(all(s.sdf(q) > 0 for s in shapes) for q in points), (name, gap["id"])
    assert seen


def test_seed_determines_the_tree(repo_root):
    p, built, data = quick(repo_root, "V01_S")
    fit, shell.fit = shell.fit, no_shell
    try:
        again = tree.build(p)
        other = tree.build(dict(p, seed=p["seed"] + 1))
    finally:
        shell.fit = fit
    assert again["lods"][0].vertices == built["lods"][0].vertices
    assert meta.assemble("V01", "V01_S", "tree", p, again, VISUALS) == data
    assert other["lods"][0].vertices != built["lods"][0].vertices
