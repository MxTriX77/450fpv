"""generators.debris_timber and lib.debris_timber: the K03 sawn-timber pieces (p1-h01-adobe-house, design decision 10;
the spec's "K02 tile shards and K03 timber pieces"): the sizes within 5 mm, the section split into splinters that
tile it, the splinters (tapered, splayed, never under the ground, longest on the tension face, the longest reaching the
piece's end), each with its own capsule and `hook` snag, the broken ends' `edge` snags, the nails' capsules and hooks,
the slots and the grain UVs, the "not built yet" and unused-key refusals, determinism, each piece's audit within the
2 cm zone's tolerance, and pieces composed by a host audited with it."""
import copy
import math
import random

import pytest

from generators import debris_timber as generator
from lib import audit, catalog, debris_timber, kits_vehicle, meta, params as params_lib
from lib.geometry import Mesh

VISUALS = {"timber": "roof_timber", "timber_fracture": "timber_fracture", "nail": "car_rust"}
MM5 = 0.005
EPS = 1e-9


@pytest.fixture(scope="module")
def k03():
    from pathlib import Path
    root = Path(__file__).resolve().parents[3]
    entry = catalog.load(root)["assets"]["K03"]
    assert entry["generator"] == "debris_timber" and entry["materials"] == VISUALS
    return {v: catalog.load_params(catalog.params_path(root, entry, v)) for v in entry["variants"]}


def test_catalog_lists_the_three_pieces(k03):
    assert sorted(k03) == ["K03_batten_broken", "K03_board", "K03_rafter_broken"]
    for variant, params in k03.items():
        assert params_lib.validate(generator, params) == [], variant
        assert params["physics_material"] == "timber"
    sizes = {v: (p["piece"], p["section_m"]["w"], p["section_m"]["h"], p["break_ends"]) for v, p in k03.items()}
    assert sizes == {"K03_batten_broken": ("batten", 0.05, 0.05, 2), "K03_board": ("board", 0.15, 0.032, 1),
                     "K03_rafter_broken": ("rafter_sawn", 0.15, 0.10, 1)}


@pytest.mark.parametrize("variant", ["K03_batten_broken", "K03_board", "K03_rafter_broken"])
def test_sizes_within_5_mm(k03, variant):
    """Tip to tip `length_m`, the section across and up (the nails standing over it), the body solid between the
    breaks, lying on y = 0."""
    params = k03[variant]
    built = generator.build(params)
    lo, hi = built["lods"][0].bounds()
    w, h = params["section_m"]["w"], params["section_m"]["h"]
    assert hi[0] - lo[0] == pytest.approx(params["length_m"], abs=1e-9)
    assert hi[2] - lo[2] == pytest.approx(w, abs=MM5) and lo[1] == pytest.approx(0.0, abs=1e-12)
    over = max(params["nail_proud_mm"] / 1000 + debris_timber.NAIL_R_M, debris_timber.SPLAY * params["splinter_len_m"])
    assert hi[1] <= h + over + EPS  # a nail's tip ring or an upper splinter's splay
    body = built["collision"][0]
    assert body["shape"] == "box" and body["size"][1:] == [h, w]
    assert body["size"][0] == pytest.approx(params["length_m"] - params["break_ends"] * params["splinter_len_m"])


def test_the_section_splits_into_cells_that_tile_it():
    for seed in range(20):
        for w, h, count in ((0.05, 0.05, 5), (0.15, 0.032, 5), (0.15, 0.10, 8), (0.10, 0.025, 10)):
            cells = debris_timber.split_section(random.Random(seed), w, h, count)
            assert len(cells) == count
            assert sum((z1 - z0) * (y1 - y0) for z0, z1, y0, y1 in cells) == pytest.approx(w * h)
            assert all(-w / 2 - EPS <= z0 < z1 <= w / 2 + EPS and -EPS <= y0 < y1 <= h + EPS for z0, z1, y0, y1 in cells)
            for i, a in enumerate(cells):  # no two overlap
                for b in cells[i + 1:]:
                    assert min(a[1], b[1]) - max(a[0], b[0]) <= EPS or min(a[3], b[3]) - max(a[2], b[2]) <= EPS


@pytest.mark.parametrize("seed", range(1, 13))
def test_splinters(seed):
    """Each tapers from its cell to a tip TAPER of it, splayed outward by at most SPLAY of its length, never under
    the underside; the longest is the end's splinter length; the tension face's are longer than the other face's."""
    w, h, length = 0.15, 0.10, 0.3
    broken = debris_timber.splinters(random.Random(seed), w, h, length, 8)
    assert max(s["length"] for s in broken) == pytest.approx(length)
    j0, j1 = debris_timber.LENGTH_JITTER
    for s in broken:
        z0, z1, y0, y1 = s["cell"]
        (tz, ty), (hz, hy) = s["tip"], s["tip_half"]
        assert debris_timber.TAPER[0] * (z1 - z0) / 2 - EPS <= hz <= debris_timber.TAPER[1] * (z1 - z0) / 2 + EPS
        assert debris_timber.TAPER[0] * (y1 - y0) / 2 - EPS <= hy <= debris_timber.TAPER[1] * (y1 - y0) / 2 + EPS
        assert ty - hy >= -EPS, "a tip under the underside"
        assert abs(tz - (z0 + z1) / 2) <= debris_timber.SPLAY * s["length"] + EPS
        assert ty == hy or abs(ty - (y0 + y1) / 2) <= debris_timber.SPLAY * s["length"] + EPS
        assert s["length"] >= length * debris_timber.SHORTEST * j0 / j1 - EPS
    # The bias: on one face taken as the tension face, every length over its weight lies within the jitter's band.
    bands = []
    for tension_top in (True, False):
        ratios = []
        for s in broken:
            share = (s["cell"][2] + s["cell"][3]) / 2 / h
            ratios.append(s["length"] / (debris_timber.SHORTEST + (1 - debris_timber.SHORTEST) *
                                         (share if tension_top else 1 - share)))
        bands.append(max(ratios) / min(ratios))
    assert min(bands) <= j1 / j0 + EPS


def test_broken_ends_carry_their_splinters_and_snags(k03):
    """Per broken end: a capsule and a `hook` per splinter from root to tip at its half-thickness, an `edge` snag
    round the root's section; a sawn end none; the splinter faces kept from the piece's outside are timber, those the
    break opened timber_fracture."""
    for variant in k03:
        params = k03[variant]
        built = generator.build(params)
        count, ends, nails = params["splinter_count"], params["break_ends"], params["nails"]
        shapes = [c["shape"] for c in built["collision"]]
        assert shapes == ["box"] + ["capsule"] * (count * ends + 2 * nails)
        kinds = [s["kind"] for s in built["snag"]]
        assert kinds == (["hook"] * count + ["edge"]) * ends + ["hook"] * nails
        edges = [s for s in built["snag"] if s["kind"] == "edge"]
        assert all(len(s["points"]) == 5 and s["points"][0] == s["points"][-1] and s["radius_m"] == 0.002 for s in edges)
        root_x = sorted({s["points"][0][0] for s in edges}, reverse=True)
        half = params["length_m"] / 2 - params["splinter_len_m"]
        assert root_x == pytest.approx([half, -half][:ends])
        hooks = [s for s in built["snag"] if s["kind"] == "hook" and len(s["points"]) == 2]
        assert len(hooks) == count * ends and all(s["points"][0][0] in (half, -half) for s in hooks)
        assert max(abs(s["points"][1][0]) for s in hooks) == pytest.approx(params["length_m"] / 2)
        capsules = built["collision"][1:1 + count * ends]
        assert all(c["material"] == "timber" and c["landable"] is False for c in capsules)
        assert [m["slot"] for m in built["materials"]] == ["timber", "timber_fracture", "nail"]
        assert [m["physics"] for m in built["materials"]] == ["timber", "timber", "steel"]
        for indices, normal, uvs, slot in built["lods"][0].faces:
            if slot != "timber" or abs(normal[0]) > 0.99:
                continue
            on = [built["lods"][0].vertices[i] for i in indices]
            outer = any(abs(p[1]) < EPS or abs(p[1] - params["section_m"]["h"]) < EPS or
                        abs(abs(p[2]) - params["section_m"]["w"] / 2) < EPS for p in on)
            assert outer, "a timber face of a splinter lies on the piece's outside"


def test_nails(k03):
    """Two capsules of 1.75 mm per nail, steel, from just inside the top face through the bend to the tip, the tip
    at most nail_proud_mm over the face; a `hook` through root, bend and tip; at least NAIL_END_M from the body's ends."""
    params = k03["K03_board"]
    built = generator.build(params)
    h, proud = params["section_m"]["h"], params["nail_proud_mm"] / 1000
    nails = [s for s in built["snag"] if s["kind"] == "hook" and len(s["points"]) == 3]
    assert len(nails) == params["nails"]
    capsules = built["collision"][-2 * len(nails):]
    assert all(c["shape"] == "capsule" and c["radius"] == pytest.approx(0.00175) and c["material"] == "steel"
               for c in capsules)
    body = built["collision"][0]
    x_lo, x_hi = body["center"][0] - body["size"][0] / 2, body["center"][0] + body["size"][0] / 2
    for s in nails:
        root, bend, tip = s["points"]
        assert root[1] == pytest.approx(h) and s["radius_m"] == pytest.approx(0.00175)
        assert math.dist(root, bend) + math.dist(bend, tip) == pytest.approx(proud)
        assert tip[1] > h and x_lo + debris_timber.NAIL_END_M - EPS <= root[0] <= x_hi - debris_timber.NAIL_END_M + EPS


def test_grain_runs_along_the_piece_and_round_its_section(k03):
    """V is x plus the piece's offset on every long face, U continues round the section across its arrises."""
    built = generator.build(k03["K03_batten_broken"])
    lod0 = built["lods"][0]
    offsets_v, round_u = set(), {}
    for indices, normal, uvs, slot in lod0.faces[:4]:  # the body's four long faces
        for i, uv in zip(indices, uvs):
            p = lod0.vertices[i]
            offsets_v.add(round(uv[1] - p[0], 9))
            round_u.setdefault((round(p[1], 9), round(p[2], 9)), set()).add(round(uv[0], 9))
    assert len(offsets_v) == 1
    seams = [u for u in round_u.values() if len(u) > 1]
    assert len(seams) == 1, "one seam round the section, at the underside's -z arris"


@pytest.mark.parametrize("edit, expected", [
    (lambda p: p.update(piece="beam"), "'piece' = 'beam' is not built yet (built: board, batten, rafter_sawn)"),
    (lambda p: p.update(piece="log_section"), "'piece' = 'log_section' is not built yet (built: board, batten, rafter_sawn)"),
    (lambda p: p.update(piece="rafter_round"), "'piece' = 'rafter_round' is not built yet (built: board, batten, rafter_sawn)"),
    (lambda p: p.update(piece="panel"), "'piece' = 'panel' is not built yet (built: board, batten, rafter_sawn)"),
    (lambda p: p.update(piece="joist"),
     "'piece' = 'joist' must be one of board, batten, rafter_sawn, rafter_round, beam, panel, log_section"),
    (lambda p: p["section_m"].update(h=0.06), "'section_m.h' = 0.06 must lie in [0.025, 0.04] for a board"),
    (lambda p: p.update(break_ends=0), "'splinter_len_m' = 0.2 is not used without a broken end; set it to 0"),
    (lambda p: p.update(nails=0), "'nail_proud_mm' = 50.0 is not used without nails; set it to 0"),
    (lambda p: p.update(nail_proud_mm=10.0), "'nail_proud_mm' = 10.0 must lie in [20.0, 80.0]"),
    (lambda p: p.update(splinter_count=1), "'splinter_count' = 1 must lie in [2, 10] for a broken end"),
    (lambda p: p.update(length_m=0.3, nails=0, nail_proud_mm=0.0, splinter_len_m=0.25),
     "'length_m' = 0.3 leaves 0.050 m of solid body between the breaks, under 0.1 m"),
    (lambda p: p.update(physics_material="deadwood"), "'physics_material' = 'deadwood' must be 'timber'"),
])
def test_refusals(k03, edit, expected):
    params = copy.deepcopy(k03["K03_board"])
    edit(params)
    problems = params_lib.validate(generator, params)
    assert expected in problems, problems


def test_a_board_lies_on_its_wide_face(k03):
    params = copy.deepcopy(k03["K03_rafter_broken"])
    params["section_m"] = {"w": 0.10, "h": 0.15}
    assert "'section_m.h' = 0.15 exceeds 'section_m.w' = 0.1: the piece lies on its wide face" in \
        params_lib.validate(generator, params)


def test_seed_moves_the_splinters_and_nails(k03):
    params = k03["K03_batten_broken"]
    a = generator.build(params)["lods"][0].vertices
    assert a == generator.build(params)["lods"][0].vertices
    assert a != generator.build(dict(params, seed=2))["lods"][0].vertices


def test_lod1_drops_the_nails_and_merges_the_splinters(k03):
    for variant, params in k03.items():
        lod0, lod1 = generator.build(params)["lods"]
        assert lod1.triangles() * 2 <= lod0.triangles(), variant
        assert "nail" not in lod1.slots() and lod1.bounds()[0][0] == pytest.approx(lod0.bounds()[0][0], abs=0.4)


@pytest.mark.parametrize("variant", ["K03_batten_broken", "K03_board", "K03_rafter_broken"])
@pytest.mark.parametrize("seed", [1, 3, 7])
def test_each_piece_passes_the_audit_within_the_near_zone(k03, variant, seed, material_ids):
    params = dict(k03[variant], seed=seed)
    built = generator.build(params)
    data = meta.assemble("K03", variant, "debris_timber", params, built, VISUALS)
    assert meta.validate(data, material_ids) == []
    problems, lines = audit.run(data, built["lods"][0], planted=True)
    assert problems == []
    values = [float(line.split("->")[1].split()[1]) for line in lines if "->" in line]
    assert values and max(values) <= audit.NEAR_TOLERANCE_M * 1000


def test_pieces_compose_into_a_host_audited_with_it(k03, material_ids):
    """The pipeline's "Composed kit pieces": a batten, a board and a rafter placed by a host, leaning and turned,
    each with its own primitives and snags, audited as one asset."""
    host = Mesh()
    collision, snag = [], []
    for k, params in enumerate(k03.values()):
        piece = generator.build(dict(params, seed=k + 4))
        rigid = kits_vehicle.axis_rotation((0, 0, 0), (0.0, 0.0, 1.0), 0.4 * k).then(
            kits_vehicle.axis_rotation((0, 0, 0), (0.0, 1.0, 0.0), 0.9 * k)).then(
            kits_vehicle.translation((0.0, 0.5 + 0.4 * k, 0.6 * k)))
        kits_vehicle.append_mesh(host, piece["lods"][0], rigid)
        collision += [kits_vehicle.transform_shape(c, rigid) for c in piece["collision"]]
        snag += [kits_vehicle.transform_snag(s, rigid) for s in piece["snag"]]
    physics = {"timber": "timber", "timber_fracture": "timber", "nail": "steel"}
    built = {"lods": [host], "switch_distances_m": [0.0], "cull_distance_m": 300.0,
             "materials": [{"slot": s, "physics": physics[s]} for s in host.slots()], "collision": collision,
             "gaps": [], "wind_volumes": [], "snag": snag}
    data = meta.assemble("H00", "H00_test", "house", {}, built, VISUALS)
    assert meta.validate(data, material_ids) == []
    problems, lines = audit.run(data, host)
    assert problems == [], problems
    assert sum(1 for line in lines if line.startswith("audit collision[")) == len(collision)
