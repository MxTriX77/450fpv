"""The hunter-09 package (tools/map/compile_track.py, w20-hunter-track 1.4): the spec's ground and doubled-rows scenarios
on the committed package, and a byte-identical recompile. The loaded world's scenarios (the gaps, what hangs on the
houses) are the sim's: unity/Packages/com.fpv450.sim/Tests/World/HunterTrackTests.cs."""
import json
import math
import shutil
import sys
from array import array
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import compile_track as ct  # noqa: E402
import validate as v  # noqa: E402

MODELS = ct.REPO / "unity" / "Assets" / "_Project" / "Art" / "Models" / "track" / "T01"
GATES = ("frame_8x10", "frame_square_a", "frame_square_b", "light_gate")
FOOT_TOLERANCE_M = 0.05    # the spec
BAND_LIFT_M = 0.010        # the kit lifts a ribbon's band this far above its points (assets-src/track, band_lift_m)
GATE_TOLERANCE_M = 0.001   # the opening's lower edge may rest on the ground, to the sample's rounding
BODY_OVER_GROUND_M = 0.234 # drone.json: the legs' tips under the body's centre
PROP_REACH_M = 0.353       # drone.json: a hub 0.226 m from the centre, plus the prop's 0.127 m
BLOCK_CENTRE_M = (0.0, 0.381, 0.0)  # T01_concrete_block's collision box
BLOCK_HALF_M = (0.381, 0.381, 0.762)


@pytest.fixture(scope="module")
def package():
    manifest = json.loads((ct.ROOT / "map.json").read_text(encoding="utf-8"))
    samples = array("H")
    samples.frombytes((ct.ROOT / "heights" / "map.r16").read_bytes())
    tile = v.Tile(manifest["tiles"][0], samples)
    layout = json.loads((ct.ROOT / "layout.json").read_text(encoding="utf-8"))
    return {"tile": tile, "params": ct.load_params(ct.ROOT / "params.json"), "layout": layout,
            "rows": ct.distinct(layout["objects"]),
            "placements": json.loads((ct.ROOT / "placements.json").read_text(encoding="utf-8"))}


def turn(q, p):
    x, y, z, w = q
    tx, ty, tz = 2 * (y * p[2] - z * p[1]), 2 * (z * p[0] - x * p[2]), 2 * (x * p[1] - y * p[0])
    return p[0] + w * tx + y * tz - z * ty, p[1] + w * ty + z * tx - x * tz, p[2] + w * tz + x * ty - y * tx


def test_the_package_validates():
    errors, summary = v.validate(ct.ROOT)
    assert errors == [], errors[:5]
    assert "275 track" in summary and "1000 height probes" in summary


def test_a_recompile_is_byte_identical(tmp_path):
    root = tmp_path / ct.MAP_ID
    root.mkdir()
    for name in ("layout.json", "params.json", "height_probes.json"):
        shutil.copy(ct.ROOT / name, root / name)
    vendor = tmp_path / "vendor_track.json"
    ready, _ = ct.compile_package(root, vendor)
    assert ready, "the committed probes do not answer the committed heights"
    assert vendor.read_bytes() == ct.VENDOR_TRACK.read_bytes()
    for name in ("map.json", "placements.json", "outlines.json", "height_probes.json", "heights/map.r16", "classes/map.raw",
                 "grass/map.raw"):
        assert (root / name).read_bytes() == (ct.ROOT / name).read_bytes(), name


def test_every_distinct_drawn_row_stands_once(package):
    assert len(package["layout"]["objects"]) == 334
    rows = package["placements"]["placements"]
    drawn = [r for r in package["rows"] if r["drawn"]]
    placed = [r for r in rows if "layout_id" in r]
    assert len(drawn) == len(placed) == 272
    assert sorted(r["layout_id"] for r in placed) == sorted(r["instance_id"] for r in drawn)
    by_id = {r["instance_id"]: r for r in drawn}
    assert all(r["position_m"] == by_id[r["layout_id"]]["position_m"]
               and r["rotation_xyzw"] == by_id[r["layout_id"]]["rotation_xyzw"] for r in placed)
    assert sorted(r["variant"] for r in rows if "layout_id" not in r) == ["T02_north", "T02_north_east", "T02_south"]


def test_the_32_upright_feet_meet_the_ground(package):
    feet = ct.foot_rows(package["rows"], package["params"])
    assert len(feet) == 32
    worst = max(abs(o["position_m"][1] - package["tile"].height(o["position_m"][0], o["position_m"][2])) for o in feet)
    assert worst <= FOOT_TOLERANCE_M


def test_all_10_gate_openings_are_above_the_ground(package):
    gates = [o for o in package["rows"] if o["kind"] in GATES]
    assert len(gates) == 10
    for o in gates:
        meta = json.loads((MODELS / f"T01_{o['kind']}.assetmeta.json").read_text(encoding="utf-8"))
        corners = [[o["position_m"][k] + turn(o["rotation_xyzw"], c)[k] for k in range(3)] for c in meta["gaps"][0]["polygon"]]
        for a, b in zip(corners, corners[1:] + corners[:1]):
            for i in range(21):
                q = [a[k] + (b[k] - a[k]) * i / 20 for k in range(3)]
                assert q[1] >= package["tile"].height(q[0], q[2]) - GATE_TOLERANCE_M, f"gate {o['instance_id']}"


def test_nothing_resting_on_the_ground_is_buried(package):
    tile = package["tile"]
    for o in ct.resting_rows(package["rows"], package["params"]):
        assert tile.height(o["position_m"][0], o["position_m"][2]) - o["position_m"][1] <= FOOT_TOLERANCE_M, o["instance_id"]
    for run in ct.ribbon_runs(package["rows"], package["params"]):
        for a, b in zip(run, run[1:]):
            n = max(1, int(math.dist(a, b) / 0.1))
            for i in range(n + 1):
                q = [a[k] + (b[k] - a[k]) * i / n for k in range(3)]
                assert tile.height(q[0], q[2]) - q[1] <= BAND_LIFT_M, f"ribbon at {q}"


def test_the_ground_has_the_sheets_dips_and_house_levels(package):
    tile, p = package["tile"], package["params"]
    sample = ct.MAX_SAMPLE
    rounding = p["tile_height_m"] / sample
    for dip in p["dips"]:
        assert abs(tile.height(*dip["centre_m"]) - dip["floor_m"]) <= rounding, dip["id"]
    for row in p["houses"]:
        house = ct.HouseGround(row)
        for u in (0.5, house.length / 2, house.length - 0.5):
            for s in (house.v0 + 0.1, house.width / 2, house.width - 0.1):
                x = row["ox"] + u * house.sin + s * house.cos
                z = row["oz"] + u * house.cos - s * house.sin
                assert abs(tile.height(x, z) - house.floor(u)) <= 2 * rounding, (row["variant"], u, s)


def test_the_start_is_the_races_spawn_point_upright(package):
    starts = package["placements"]["start_points"]
    spawn = next(o for o in package["rows"] if o["kind"] == "spawn_point"
                 and o["instance_id"] == package["layout"]["race"]["spawn_point_instance_id"])
    assert [s["id"] for s in starts] == ["spawn"]
    forward = turn(spawn["rotation_xyzw"], (0.0, 0.0, 1.0))
    assert starts[0]["heading_deg"] == pytest.approx(math.degrees(math.atan2(forward[0], forward[2])) % 360.0, abs=0.001)
    along = math.atan2(forward[0], forward[2])
    moved = package["params"]["start_forward_m"]
    assert starts[0]["position_m"][0] == pytest.approx(spawn["position_m"][0] + moved * math.sin(along), abs=1e-5)
    assert starts[0]["position_m"][2] == pytest.approx(spawn["position_m"][2] + moved * math.cos(along), abs=1e-5)


def test_the_start_stands_clear_of_every_concrete_block(package):
    """The pilot's first flight: the file's spawn stands on a block, a flight starts on the terrain, and a drone started
    inside the block is thrown out on its first step. The body's centre, a leg's length over the ground, clears each."""
    x, _, z = package["placements"]["start_points"][0]["position_m"]
    body = (x, package["tile"].height(x, z) + BODY_OVER_GROUND_M, z)
    for block in (o for o in package["rows"] if o["kind"] == "concrete_block"):
        qx, qy, qz, qw = block["rotation_xyzw"]
        local = turn((-qx, -qy, -qz, qw), [body[a] - block["position_m"][a] for a in range(3)])
        outside = [max(abs(local[a] - BLOCK_CENTRE_M[a]) - BLOCK_HALF_M[a], 0.0) for a in range(3)]
        assert math.sqrt(sum(d * d for d in outside)) > PROP_REACH_M, f"block {block['instance_id']}"


# ---- the grass (task 3.2a)


@pytest.fixture(scope="module")
def grass():
    manifest = json.loads((ct.ROOT / "map.json").read_text(encoding="utf-8"))
    return manifest["grass"], (ct.ROOT / manifest["grass"]["file"]).read_bytes()


def test_the_grass_is_villages_layers_at_its_meadow_density(package, grass):
    """Village's 14 layers as its package lists them, and each layer's mean over the cells that grow grass is the
    params' measured meadow mean (a cell holds the whole part and one more with the fraction's chance)."""
    entry, data = grass
    village = json.loads((ct.REPO / package["params"]["grass_from"]).read_text(encoding="utf-8"))["grass"]
    assert entry["resolution"] == village["resolution"] == package["params"]["grass_resolution"]
    assert [{k: v for k, v in e.items() if k != "instances"} for e in entry["layers"]] == \
           [{k: v for k, v in e.items() if k != "instances"} for e in village["layers"]]
    plane = entry["resolution"] ** 2
    bare = [cell for cell in range(plane) if not any(data[l * plane + cell] for l in range(len(entry["layers"])))]
    assert len(bare) < plane // 500, "grass grows everywhere but round the objects"
    growing = plane - len(bare)
    for layer, mean in enumerate(package["params"]["grass_per_cell"]):
        counts = data[layer * plane:(layer + 1) * plane]
        whole = math.floor(mean)
        assert sum(counts) == entry["layers"][layer]["instances"]
        assert all(whole <= counts[cell] <= whole + 1 for cell in range(0, plane, 997) if cell not in bare)
        assert sum(counts) / growing == pytest.approx(mean, abs=0.002), f"layer {layer}"


def test_no_grass_inside_the_houses(package, grass):
    entry, data = grass
    res, plane = entry["resolution"], entry["resolution"] ** 2
    side = 1000 / res
    for row in package["params"]["houses"]:
        house = ct.HouseGround(row)
        for i in range(int(house.length / 0.25) + 1):
            for j in range(int((house.width - house.v0) / 0.25) + 1):
                u, s = i * 0.25, house.v0 + j * 0.25
                x = row["ox"] + u * house.sin + s * house.cos
                z = row["oz"] + u * house.cos - s * house.sin
                cell = int((1000 - z) // side) * res + int(x // side)
                assert not any(data[l * plane + cell] for l in range(len(entry["layers"]))), (row["variant"], u, s)


def test_no_grass_within_the_clearance_of_any_placed_object(package, grass):
    """Every corner of each placed row's collision shapes and every point of its snag lines, seen from above, and the
    points the clearance away from each, lie in bare cells (independent of the compiler's outline arithmetic)."""
    entry, data = grass
    res, plane = entry["resolution"], entry["resolution"] ** 2
    side, clear = 1000 / res, package["params"]["grass_clear_m"] - 1e-6
    catalog = json.loads(ct.CATALOG.read_text(encoding="utf-8"))["assets"]
    metas = {}
    for row in package["placements"]["placements"]:
        asset = row["variant"][:3]
        meta = metas.get(row["variant"]) or metas.setdefault(row["variant"], json.loads(
            (ct.MODELS / catalog[asset]["class"] / asset / f"{row['variant']}.assetmeta.json").read_text(encoding="utf-8")))
        points = []
        for c in meta["collision"]:
            half = [x / 2 for x in c["size"]] if c["shape"] == "box" else [c["radius"]] * 3  # inside a round shape
            points.append(c["center"])
            for k in range(8):
                local = turn(c.get("rotation", [0, 0, 0, 1]), [half[a] * (1 if k >> a & 1 else -1) for a in range(3)])
                points.append([c["center"][a] + local[a] for a in range(3)])
        points += [q for snag in meta.get("snag", []) for q in snag["points"]]
        for q in points:
            x, _, z = (row["position_m"][a] + v for a, v in enumerate(turn(row["rotation_xyzw"], q)))
            for dx, dz in ((0, 0), (clear, 0), (-clear, 0), (0, clear), (0, -clear)):
                cell = int((1000 - z - dz) // side) * res + int((x + dx) // side)
                assert not any(data[l * plane + cell] for l in range(len(entry["layers"]))), (row["id"], row["variant"])
