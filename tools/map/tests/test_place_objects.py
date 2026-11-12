"""Our objects on the farmland (tools/map/place_objects.py, w5-farmland-objects 1.2): the stream, the plan geometry, the
placements' rules, and the package's rows, hole mask and footprints checked as validate.py checks them, each break
caught."""
import copy
import json
import math
import re
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import place_objects as po  # noqa: E402
import validate as v  # noqa: E402

FARMLAND = v.REPO / "maps" / "farmland"


# ------------------------------------------------------------------ the stream and the geometry

def test_the_stream_is_splitmix64():
    """Seed 0 and no name: SplitMix64's published first outputs."""
    stream = po.Stream(0, "")
    assert [stream.next() for _ in range(3)] == [0xE220A8397B1DCDAF, 0x6E789E6AA1B965F4, 0x06C45D188009454F]
    assert po.Stream(90, "H01_I/village").next() != po.Stream(90, "H01_D/village").next(), "one stream per object"
    assert 0.0 <= min(po.Stream(1, "x").uniform() for _ in range(100)) and max(po.Stream(1, "x").uniform() for _ in range(100)) < 1.0


def test_a_yaw_turns_as_unitys_euler():
    """Unity's frame is left-handed: yaw 90 turns +X to -Z (east to south) and +Z to +X."""
    q = po.unit(po.yaw_rotation(90.0))
    assert po.rotate(q, (1.0, 0.0, 0.0)) == pytest.approx((0.0, 0.0, -1.0), abs=1e-6)
    assert po.rotate(q, (0.0, 0.0, 1.0)) == pytest.approx((1.0, 0.0, 0.0), abs=1e-6)


def test_a_footprints_distance_is_signed_from_its_outline():
    row = {"position_m": [10.0, 0.0, 20.0], "rotation_xyzw": po.yaw_rotation(0.0)}
    f = po.Footprint(row, {"min": [-2.0, 0.0, -1.0], "max": [2.0, 1.0, 1.0]})
    assert f.distance(10.0, 20.0) == pytest.approx(-1.0)
    assert f.distance(15.0, 20.0) == pytest.approx(3.0)
    assert f.distance(15.0, 25.0) == pytest.approx(5.0)  # off the corner (12, 21): hypot(3, 4)


def test_polygon_distance():
    square = [(0, 0), (1, 0), (1, 1), (0, 1)]
    assert po.polygon_distance(square, [(3, 0), (4, 0), (4, 1), (3, 1)]) == pytest.approx(2.0)
    assert po.polygon_distance(square, [(0.5, 0.5), (0.6, 0.5), (0.6, 0.6)]) == 0.0, "inside"
    assert po.polygon_distance(square, [(-1, 0.5), (2, 0.5)]) == 0.0, "a segment across"
    assert po.polygon_distance(square, [(2, 2), (3, 3)]) == pytest.approx(math.sqrt(2))


def test_the_start_is_the_players():
    """START_XZ is FlightWorlds.FarmlandStart (D-092)."""
    source = (v.REPO / "unity/Assets/_Project/Scripts/App/Session/FlightWorlds.cs").read_text(encoding="utf-8")
    found = re.search(r"FarmlandStart = new StartPoint \{[^}]*X = ([\d.]+), Z = ([\d.]+)", source)
    assert found and (float(found.group(1)), float(found.group(2))) == po.START_XZ


# ------------------------------------------------------------------ the committed package

def _farmland_checked_out():
    sample = FARMLAND / "heights" / "map.r16"
    return sample.is_file() and not sample.read_bytes()[:64].startswith(v.LFS_POINTER)


needs_farmland = pytest.mark.skipif(not _farmland_checked_out(), reason="maps/farmland's LFS files are not checked out")


@pytest.fixture(scope="module")
def farmland():
    """The package as the placer reads it, with its rows, mask, footprints document and seed."""
    package = po.Package(FARMLAND)
    rows = json.loads((FARMLAND / "placements.json").read_text(encoding="utf-8"))["placements"]
    mask = (FARMLAND / po.HOLES_FILE).read_bytes()
    document = json.loads((FARMLAND / po.FOOTPRINTS_FILE).read_text(encoding="utf-8"))
    seed = json.loads((FARMLAND / "map.json").read_text(encoding="utf-8"))["objects"]["seed"]
    return package, rows, mask, document, seed


@needs_farmland
def test_the_committed_objects_pass(farmland):
    package, rows, mask, document, seed = farmland
    assert seed == po.SEED
    assert po.check(package, rows, mask, document, seed) == []
    ours = [r for r in rows if r["variant"] in po.OURS]
    assert [r["variant"] for r in ours] == [po.TRENCH] * 11 + list(po.VILLAGE)
    assert [r["id"] for r in ours] == list(range(1679, 1694)), "after the export's 1,679 rows"


@needs_farmland
def test_the_placements_keep_the_exports_bytes(farmland):
    """placements.json is the export's text with our variants and rows added: rendering it gives its bytes, and the
    export's rows come first, unchanged."""
    raw = (FARMLAND / "placements.json").read_bytes()
    document = json.loads(raw)
    assert po.render_placements(document).encode("utf-8") == raw
    assert [r["id"] for r in document["placements"][:1679]] == list(range(1679))
    assert {r["kind"] for r in document["placements"][:1679]} == {"tree", "bush", "ruin", "debris"}


@needs_farmland
def test_one_trench_a_field_and_the_village_ones_clear(farmland):
    package, rows, _, _, _ = farmland
    ours = [r for r in rows if r["variant"] in po.OURS]
    for name, outline in package.fields.items():
        inside = [r for r in ours if po.inside_field(po.Footprint(r, package.metas[r["variant"]]["bounds"]), outline)]
        assert len(inside) == 1 and inside[0]["variant"] == po.TRENCH, name
    (cx, cz), radius = package.village
    for row in ours[11:]:
        f = po.Footprint(row, package.metas[row["variant"]]["bounds"])
        assert all(math.hypot(x - cx, z - cz) <= radius for x, z in f.corners), row["variant"]
        assert f.distance(*po.START_XZ) >= po.REACH_M + package.diagonal + package.stand_m


@needs_farmland
def test_houses_face_the_lane(farmland):
    _, rows, _, _, _ = farmland
    for row in (r for r in rows if r.get("kind") == "house"):
        yaw = math.degrees(2 * math.atan2(row["rotation_xyzw"][1], row["rotation_xyzw"][3])) % 360
        facing = 0.0 if row["position_m"][2] >= po.LANE_Z else 180.0
        assert abs((yaw - facing + 180) % 360 - 180) <= po.HOUSE_YAW_JITTER_DEG


@needs_farmland
def test_the_mask_is_rule_2(farmland):
    """Every hole cell meets an opening and lies within a band's reach of it; every other cell near a trench doesn't
    meet one (the mask brute-forced over the cells round each trench)."""
    package, rows, mask, _, _ = farmland
    n, cell = package.n, package.cell
    lines = po.openings(package, [r for r in rows if r.get("variant") == po.TRENCH])
    holes = 0
    for _, reach, _, line in lines[::3]:  # each trench's run
        xs, zs = [p[0] for p in line], [p[1] for p in line]
        for cx in range(int(min(xs) / cell) - 4, int(max(xs) / cell) + 5):
            for cz in range(int(min(zs) / cell) - 4, int(max(zs) / cell) + 5):
                square = (cx * cell, cz * cell, (cx + 1) * cell, (cz + 1) * cell)
                meets = any(po.square_to_line(square, other) <= r for _, r, _, other in lines)
                assert (mask[(n - 1 - cz) * n + cx] == 255) == meets, (cx, cz)
                holes += meets
    assert holes == mask.count(255) > 11 * 2 * 24 / cell


@needs_farmland
@pytest.mark.parametrize("break_it, message", [
    (lambda rows, mask, doc: rows[-1]["position_m"].__setitem__(0, rows[-1]["position_m"][0] + 1.0), "aren't the ones seed"),
    (lambda rows, mask, doc: rows.pop(), "aren't the ones seed"),
    (lambda rows, mask, doc: mask.__setitem__(mask.index(255), 0), "differs from the mask the trenches derive"),
    (lambda rows, mask, doc: doc["footprints"][3]["samples"][5][2].__setitem__(0, 1), "differs from the footprints the rows derive"),
])
def test_each_break_is_caught(farmland, break_it, message):
    package, rows, mask, document, seed = farmland
    rows, mask, document = copy.deepcopy(rows), bytearray(mask), copy.deepcopy(document)
    break_it(rows, mask, document)
    assert any(message in e for e in po.check(package, rows, bytes(mask), document, seed))


@needs_farmland
def test_another_seed_places_elsewhere(farmland):
    package, rows, mask, document, seed = farmland
    assert any("aren't the ones seed 91" in e for e in po.check(package, rows, mask, document, seed + 1))


@needs_farmland
def test_the_old_bands_leave_naked_holes(farmland, monkeypatch):
    """Rule 3 tells E01's widened bands from the old ones (2.35 m, 2.1 m at the niches)."""
    package, rows, _, _, _ = farmland
    ours = [r for r in rows if r.get("variant") == po.TRENCH]
    assert po.naked_samples(package, ours) == []
    narrow = copy.deepcopy(package.metas[po.TRENCH])
    for record, width in zip(narrow["holes"], (2.35, 2.1, 2.1)):
        record["band_width_m"] = width
    monkeypatch.setitem(package.metas, po.TRENCH, narrow)
    assert len(po.naked_samples(package, ours)) > 100


@needs_farmland
def test_clear_names_what_is_in_the_way(farmland):
    package, _, _, _, _ = farmland
    ruin = next(o for o in package.others if o["kind"] == "ruin")
    x, z = ruin["pivot"]
    row = po._row("R01_A", x + 12.0, z, 0.0)
    assert any(f"reaches ruin {ruin['id']}" in r for r in po.clear(package, po.Footprint(row, package.metas["R01_A"]["bounds"]), "R01_A", []))
    row = po._row("R01_A", po.START_XZ[0] + 5.0, po.START_XZ[1], 0.0)
    assert "an edit reaches the start's stand" in po.clear(package, po.Footprint(row, package.metas["R01_A"]["bounds"]), "R01_A", [])
    corner = package.fields["F1"][0]
    row = po._row(po.TRENCH, corner[0] + 5.0, corner[1] + 5.0, 0.0)
    assert not po.inside_field(po.Footprint(row, package.metas[po.TRENCH]["bounds"]), package.fields["F1"])
