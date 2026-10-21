"""generators.debris_tile and lib.debris_tile: the K02 clay-tile pieces (p1-h01-adobe-house, design decision 10; the
spec's "K02 tile shards and K03 timber pieces"): the shard outline (inside its tile, its factory edges straight on the
tile's edges, its fractures jagged, its breaks counted, within FIT_M of its one box), the whole tile's size, camber,
nib and rest on its tail and nib, the slots, snags and cell UVs, the "not built yet" and unused-key refusals,
determinism, each piece's audit within the 2 cm zone's tolerance, and pieces composed by a host audited with it."""
import copy
import math
import random

import pytest

from generators import debris_tile as generator
from lib import audit, catalog, debris_tile, kits_vehicle, meta, params as params_lib
from lib.geometry import Mesh

TILE = debris_tile.Tile(0.36, 0.22, 0.014, 0.008)
VISUALS = {"tile": "clay_tile", "tile_fracture": "tile_fracture"}
CASES = [(seed, size, breaks) for seed in range(1, 7) for size in (0.05, 0.12, 0.2, 0.3) for breaks in range(2, 7)
         if not (breaks >= 4 and size > TILE.length - 2 * debris_tile.MARGIN_M)]
EPS = 1e-9


@pytest.fixture(scope="module")
def k02():
    from pathlib import Path
    root = Path(__file__).resolve().parents[3]
    entry = catalog.load(root)["assets"]["K02"]
    assert entry["generator"] == "debris_tile" and entry["materials"] == VISUALS
    return {v: catalog.load_params(catalog.params_path(root, entry, v)) for v in entry["variants"]}


def outline(seed, size, breaks):
    return debris_tile.shard_outline(random.Random(seed), TILE, size, breaks)


def signed_area(points):
    return sum(a[0] * b[1] - b[0] * a[1] for a, b in zip(points, points[1:] + points[:1])) / 2


def rect_distance(p, box):
    x0, z0, x1, z1 = box
    dx, dz = max(x0 - p[0], 0, p[0] - x1), max(z0 - p[1], 0, p[1] - z1)
    inside = min(p[0] - x0, x1 - p[0], p[1] - z0, z1 - p[1])
    return math.hypot(dx, dz) if (dx or dz) else -inside


def segment_distance(p, a, b):
    ab = (b[0] - a[0], b[1] - a[1])
    t = max(0.0, min(1.0, ((p[0] - a[0]) * ab[0] + (p[1] - a[1]) * ab[1]) / (ab[0] ** 2 + ab[1] ** 2)))
    return math.dist(p, (a[0] + t * ab[0], a[1] + t * ab[1]))


def test_catalog_lists_the_two_pieces(k02):
    assert sorted(k02) == ["K02_tile_shard", "K02_tile_whole"]
    for variant, params in k02.items():
        assert params_lib.validate(generator, params) == [], variant
        assert params["piece"] == variant[len("K02_"):] and params["physics_material"] == "masonry"
    assert k02["K02_tile_shard"]["size_m"] == 0.15 and k02["K02_tile_shard"]["fracture_count"] == 3
    assert k02["K02_tile_whole"]["tile_m"] == {"length": 0.36, "width": 0.22, "thickness": 0.014, "camber": 0.008}


@pytest.mark.parametrize("seed, size, breaks", CASES)
def test_shard_outline(seed, size, breaks):
    """Inside its tile and counter-clockwise; every factory edge one straight edge on the tile's own edge; the breaks
    counted as fracture sides (up to four) and chipped corners (beyond four); within FIT_M of its box both ways; its
    long side the size asked for, or the tile's when it spans it; and a fan from its area centroid stays inside it."""
    points, fracture, corners, box = outline(seed, size, breaks)
    L, W = TILE.length, TILE.width
    assert all(-L / 2 - EPS <= x <= L / 2 + EPS and -W / 2 - EPS <= z <= W / 2 + EPS for x, z in points)
    assert signed_area(points) > 0
    n = len(points)
    for i in range(n):
        if not fracture[i]:
            a, b = points[i], points[(i + 1) % n]
            on_edge = (abs(abs(a[0]) - L / 2) < EPS and abs(a[0] - b[0]) < EPS) or \
                (abs(abs(a[1]) - W / 2) < EPS and abs(a[1] - b[1]) < EPS)
            assert on_edge, f"factory edge {a} -> {b} is not on the tile's edge"
    sides = sum(1 for k, i in enumerate(corners) if fracture[i])
    chips = max(0, breaks - 4)
    assert len(corners) == 4 + chips and sides == min(breaks, 4) + chips
    assert all(rect_distance(p, box) <= debris_tile.FIT_M + EPS for p in points)
    inset = debris_tile.FIT_M + debris_tile.CHIP_M[1] / math.sqrt(2)
    box_corners = [(box[0], box[1]), (box[2], box[1]), (box[2], box[3]), (box[0], box[3])]
    for c in box_corners:
        assert min(segment_distance(c, points[i], points[(i + 1) % n]) for i in range(n)) <= inset + EPS
    long_side = max(box[2] - box[0], box[3] - box[1])
    assert min(size, L - 2 * debris_tile.MARGIN_M) - EPS <= long_side <= size + 2 * debris_tile.MARGIN_M + EPS
    cx, cz = debris_tile._area_centroid(points)
    for a, b in zip(points, points[1:] + points[:1]):
        assert (a[0] - cx) * (b[1] - cz) - (b[0] - cx) * (a[1] - cz) > 0, "the fan leaves the outline"


def test_fractures_are_jagged_and_seeded():
    """A fracture side's inner vertices, every SEG_M or so, stray up to JAG_M off the line between its corners and
    not all lie on it; the same seed gives the same outline, another seed another."""
    points, fracture, corners, _ = outline(1, 0.3, 4)
    strays = []
    for k, i in enumerate(corners):
        j = corners[(k + 1) % len(corners)]
        inner = [points[m] for m in range(i + 1, j if j > i else len(points))]
        if fracture[i] and inner:
            strays += [segment_distance(p, points[i], points[j]) for p in inner]
            assert len(inner) + 1 == max(1, round(math.dist(points[i], points[j]) / debris_tile.SEG_M))
    assert strays and max(strays) <= debris_tile.JAG_M + EPS and max(strays) > debris_tile.JAG_M / 4
    assert outline(1, 0.3, 4) == outline(1, 0.3, 4) and outline(1, 0.3, 4)[0] != outline(2, 0.3, 4)[0]


def test_two_breaks_leave_a_corner_or_a_strip():
    """Two straight tile edges either way: at a corner, or opposite when the shard nearly spans the tile already."""
    def kinds(size):
        out = set()
        for seed in range(40):
            _, fracture, corners, box = outline(seed, size, 2)
            assert sum(1 for i in corners if not fracture[i]) == 2
            spans = abs(box[3] - box[1] - TILE.width) < EPS or abs(box[2] - box[0] - TILE.length) < EPS
            out.add("strip" if spans else "corner")
        return out
    assert kinds(0.1) == {"corner"}
    assert kinds(0.34) == {"corner", "strip"}


def test_shard_piece(k02):
    """One box holding the outline; the top and underside on the camber in the tile slot, the rims of fractures in
    the fracture slot; an `edge` snag along every fracture run at the top arris; the clay in one cell, V towards the
    head; resting on y = 0 and centred on its box."""
    built = generator.build(k02["K02_tile_shard"])
    lod0, lod1 = built["lods"]
    assert [c["shape"] for c in built["collision"]] == ["box"]
    box = built["collision"][0]
    assert box["material"] == "masonry" and box["landable"] is False and box["center"][0] == box["center"][2] == 0
    lo, hi = lod0.bounds()
    assert lo[1] == pytest.approx(0.0, abs=1e-12) and box["size"][1] == pytest.approx(hi[1])
    assert box["size"][1] <= TILE.thickness + TILE.camber + EPS
    assert built["materials"] == [{"slot": "tile", "physics": "masonry"}, {"slot": "tile_fracture", "physics": "masonry"}]
    rng = random.Random("debris_tile tile_shard 1")
    cell = rng.randrange(len(debris_tile.CELLS_PX))
    points, fracture, corners, _ = debris_tile.shard_outline(rng, TILE, 0.15, 3)
    runs = debris_tile.fracture_runs(fracture)
    assert [s["kind"] for s in built["snag"]] == ["edge"] * len(runs)
    assert all(s["radius_m"] == 0.002 and len(s["points"]) == len(r) for s, r in zip(built["snag"], runs))
    (u0, v0), (u1, v1) = debris_tile.CELLS_M[cell]
    scale = debris_tile._CellUV(TILE, cell).scale
    offsets = set()
    for indices, normal, uvs, slot in lod0.faces:
        assert all(u0 - EPS <= u <= u1 + EPS and v0 - EPS <= v <= v1 + EPS for u, v in uvs)
        if abs(normal[1]) > 0.9:  # the clay: V runs with x towards the head, U with z, at one scale
            assert slot == "tile"
            offsets |= {(round(uv[1] - scale * lod0.vertices[i][0], 9), round(uv[0] - scale * lod0.vertices[i][2], 9))
                        for i, uv in zip(indices, uvs)}
    assert len(offsets) == 1
    rims = [slot for indices, normal, uvs, slot in lod0.faces if abs(normal[1]) < 1e-9]
    assert rims.count("tile_fracture") == sum(fracture) and rims.count("tile") == len(fracture) - sum(fracture)
    assert lod1.triangles() * 2 <= lod0.triangles() and lod1.slots() == lod0.slots()


def test_whole_tile(k02):
    """0.36 x 0.22 m in plan within 5 mm, 14 mm of clay on an 8 mm camber, the nib under the head; resting on the
    tail's edge and the nib's foot, the head lifted by the nib; two boxes, one per half along its chord; no snag."""
    built = generator.build(k02["K02_tile_whole"])
    lod0, lod1 = built["lods"]
    lo, hi = lod0.bounds()
    assert hi[0] - lo[0] == pytest.approx(0.36, abs=0.005) and hi[2] - lo[2] == pytest.approx(0.22, abs=1e-9)
    assert lo[1] == pytest.approx(0.0, abs=1e-12)
    lifted = debris_tile.rest_angle(TILE)
    assert lifted == pytest.approx(math.atan2(0.015, 0.34))
    tail = [v for v in lod0.vertices if v[0] < lo[0] + 0.002]
    assert min(v[1] for v in tail) == pytest.approx(0.0, abs=1e-9)
    nib_foot = [v for v in lod0.vertices if v[0] > 0.15 and abs(v[2]) <= debris_tile.NIB_M[1] / 2 + EPS]
    assert min(v[1] for v in nib_foot) == pytest.approx(0.0, abs=1e-9)
    assert TILE.thickness + debris_tile.NIB_M[2] - EPS <= hi[1] <= TILE.thickness + debris_tile.NIB_M[2] + TILE.camber
    assert [c["shape"] for c in built["collision"]] == ["box", "box"] and built["snag"] == []
    for box, side in zip(built["collision"], (-1, 1)):
        angle = 2 * math.atan2(box["rotation"][2], box["rotation"][3])
        assert angle == pytest.approx(lifted - side * math.atan2(TILE.camber, TILE.length / 2), abs=1e-9)
        assert side * box["center"][0] > 0
    assert built["materials"] == [{"slot": "tile", "physics": "masonry"}]
    assert (lod0.triangles(), lod1.triangles()) == (78, 20)


@pytest.mark.parametrize("edit, expected", [
    (lambda p: p.update(piece="ridge_piece"),
     "'piece' = 'ridge_piece' is not built yet (built: tile_shard, tile_whole)"),
    (lambda p: p.update(piece="slate_fragment"),
     "'piece' = 'slate_fragment' is not built yet (built: tile_shard, tile_whole)"),
    (lambda p: p.update(piece="pantile"),
     "'piece' = 'pantile' must be one of tile_shard, tile_whole, ridge_piece, slate_sheet, slate_half, slate_fragment, "
     "slate_stack"),
    (lambda p: p.update(size_m=0.4), "'size_m' = 0.4 must lie in [0.05, 0.35] for a tile_shard"),
    (lambda p: p.update(fracture_count=1), "'fracture_count' = 1 must lie in [2, 6] for a tile_shard"),
    (lambda p: p.update(fracture_count=4, size_m=0.34),
     "'size_m' = 0.34 does not fit inside its tile with 4 breaks (at most 0.32)"),
    (lambda p: p.update(physics_material="asbestos_cement"), "'physics_material' = 'asbestos_cement' must be 'masonry'"),
])
def test_refusals(k02, edit, expected):
    params = copy.deepcopy(k02["K02_tile_shard"])
    edit(params)
    assert params_lib.validate(generator, params) == [expected]


def test_a_whole_tile_takes_no_shard_keys(k02):
    params = copy.deepcopy(k02["K02_tile_whole"])
    params.update(size_m=0.15, fracture_count=3)
    assert params_lib.validate(generator, params) == [
        "'size_m' = 0.15 is not used by a tile_whole; set it to 0",
        "'fracture_count' = 3 is not used by a tile_whole; set it to 0"]


def test_seed_moves_the_shard(k02):
    params = k02["K02_tile_shard"]
    a = generator.build(params)["lods"][0].vertices
    assert a == generator.build(params)["lods"][0].vertices
    assert a != generator.build(dict(params, seed=2))["lods"][0].vertices


@pytest.mark.parametrize("seed, size, breaks", CASES[::5])
def test_every_shard_passes_the_audit_within_the_near_zone(seed, size, breaks, material_ids):
    params = dict(piece="tile_shard", seed=seed, size_m=size, fracture_count=breaks, physics_material="masonry",
                  tile_m=dict(TILE._asdict()), lod={"lod1_switch_m": 3.0, "cull_m": 70.0})
    _audited(params, material_ids)


@pytest.mark.parametrize("variant", ["K02_tile_shard", "K02_tile_whole"])
def test_each_variant_passes_the_audit_within_the_near_zone(k02, variant, material_ids):
    _audited(k02[variant], material_ids)


def _audited(params, material_ids):
    """The piece's audit: clean, and every value under the 2 cm zone's tolerance, so a host may put it anywhere."""
    built = generator.build(params)
    data = meta.assemble("K02", "K02_x", "debris_tile", params, built, VISUALS)
    assert meta.validate(data, material_ids) == []
    problems, lines = audit.run(data, built["lods"][0], planted=True)
    assert problems == []
    values = [float(line.split("->")[1].split()[1]) for line in lines if "->" in line]
    assert values and max(values) <= audit.NEAR_TOLERANCE_M * 1000


def test_pieces_compose_into_a_host_audited_with_it(material_ids):
    """The pipeline's "Composed kit pieces": shards and a whole tile placed by a host at their own yaw and tilt,
    each with its own box and snags, audited as one asset."""
    host = Mesh()
    collision, snag = [], []
    rng = random.Random(7)
    pieces = [debris_tile.shard(rng, TILE, rng.uniform(0.05, 0.3), rng.randint(2, 6), k % 6) for k in range(6)]
    pieces.append(debris_tile.whole(TILE, 2))
    for k, piece in enumerate(pieces):
        rigid = kits_vehicle.axis_rotation((0, 0, 0), (1.0, 0.0, 0.0), 0.3 * (k % 3) - 0.3).then(
            kits_vehicle.axis_rotation((0, 0, 0), (0.0, 1.0, 0.0), 1.1 * k)).then(
            kits_vehicle.translation((0.4 * k, 0.2, 0.3 * (k % 2))))
        kits_vehicle.append_mesh(host, piece["lods"][0], rigid)
        collision += [kits_vehicle.transform_shape(c, rigid) for c in piece["collision"]]
        snag += [kits_vehicle.transform_snag(s, rigid) for s in piece["snag"]]
    built = {"lods": [host], "switch_distances_m": [0.0], "cull_distance_m": 100.0,
             "materials": [{"slot": s, "physics": "masonry"} for s in host.slots()], "collision": collision,
             "gaps": [], "wind_volumes": [], "snag": snag}
    data = meta.assemble("H00", "H00_test", "house", {}, built, VISUALS)
    assert meta.validate(data, material_ids) == []
    assert len(data["collision"]) == 6 + 2 and len(data["snag"]) == sum(len(p["snag"]) for p in pieces)
    problems, lines = audit.run(data, host)
    assert problems == [], problems
    assert sum(1 for line in lines if line.startswith("audit collision[")) == 8
