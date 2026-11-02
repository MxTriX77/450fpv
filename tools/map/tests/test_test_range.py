"""The test range's placement file (p6-test-range, test-range spec): the contents, the posadka's shape, the start
points, the noclip start, the ground's patches and holes, the hung spans, and the generator's bytes. It reads the
committed maps/test-range/placements.json and the assets' .assetmeta.json; the scene's side is Unity's EditMode
TestRangeTests."""
import json
import math
import statistics
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import make_test_range as gen  # noqa: E402

MM = 0.001


@pytest.fixture(scope="module")
def data():
    return json.loads(gen.OUT.read_text(encoding="utf-8"))


@pytest.fixture(scope="module")
def world():
    return gen.Map(gen.Assets())


def rows(data, **match):
    return [r for r in data["instances"] if all(r[k] == v for k, v in match.items())]


def test_the_same_seed_gives_the_same_bytes(tmp_path):
    a, b = tmp_path / "a.json", tmp_path / "b.json"
    assert gen.main([str(a)]) == 0 and gen.main([str(b)]) == 0
    assert a.read_bytes() == b.read_bytes()
    assert a.read_bytes() == gen.OUT.read_bytes(), "the committed file is stale: run tools/map/make_test_range.py"


def test_everything_is_placed(data, world):
    """The spec's list: the Godot yard at its relative layout, the posadka's mix, grass both sides of the belt, both
    cars, at least four poles with spans, E01 along the belt, H01_I and H01_D beside the yard; every variant built and
    inside the map."""
    yx, yz = gen.v("yard_centre")
    yard = rows(data, group="yard")
    expected = sorted((gen.STAND_INS[k], round(yx + dx, 3), round(yz - dz, 3), round(-yaw % 360.0, 3))
                      for k, dx, dz, yaw in gen.ADOBE_YARD)
    placed = sorted((r["variant"], r["position_m"][0], r["position_m"][2], r["yaw_deg"]) for r in yard)
    assert placed == expected
    assert sum(r["variant"] == "Z07_tree_fruit" for r in yard) == 2

    houses = rows(data, asset="H01")
    assert sorted(r["variant"] for r in houses) == ["H01_D", "H01_I"], "H01's intact and damaged houses"
    hx, hz = gen.v("yard_half_m")
    yard_rect = [yx - hx, yz - hz, yx + hx, yz + hz]
    for r in houses:
        rect = world.bounds_rect(r)
        assert math.hypot(r["position_m"][0] - yx, r["position_m"][2] - yz) < 60.0, f"{r['variant']} by the yard"
        assert not gen.overlaps(rect, yard_rect), f"{r['variant']} beside the yard, not in it"
        assert rect[3] < gen.v("track_z"), f"{r['variant']}'s spread stops short of the track"

    belt = rows(data, group="posadka")
    assert {r["asset"] for r in belt} == {"Z08", "Z09", "Z10", "Z11", "Z12", "Z13", "V01", "V13"}
    belt_z = [r["position_m"][2] for r in belt]
    grass = rows(data, asset="S04")
    assert any(g["position_m"][2] < min(belt_z) for g in grass) and any(g["position_m"][2] > max(belt_z) for g in grass)

    car_a, car_f = rows(data, variant="R01_A"), rows(data, variant="R01_F")
    assert len(car_a) == 1 and len(car_f) == 1
    assert math.hypot(car_a[0]["position_m"][0] - yx, car_a[0]["position_m"][2] - yz) < 25.0, "R01_A in or by the yard"
    assert abs(car_f[0]["position_m"][2] - gen.v("track_z")) < 1.0, "R01_F on the track"

    poles = rows(data, asset="L01")
    spans = rows(data, asset="L03")
    assert len(poles) >= 4 and len(spans) == len(poles) - 1
    assert all(s["hang"]["from"] == a["id"] and s["hang"]["to"] == b["id"] for s, a, b in zip(spans, poles, poles[1:]))

    trench = rows(data, asset="E01")
    assert len(trench) == 1
    footprint = world.bounds_rect(trench[0])
    assert footprint[3] < min(belt_z) and min(belt_z) - footprint[3] < 3.0, "E01 runs along the belt's edge"
    x0, x1 = gen.v("belt_x")
    assert x0 < footprint[0] and footprint[2] < x1

    half = 0.5 * gen.v("map_side_m")
    for r in data["instances"]:
        world.assets.meta(r["asset"], r["variant"])  # built, in the catalog
        assert r["class"] == world.assets.klass(r["asset"])
        assert all(-half <= c <= half for c in (r["position_m"][0], r["position_m"][2]))
        assert r["position_m"][1] == 0.0
    assert [r["id"] for r in data["instances"]] == list(range(len(data["instances"])))


def nearest(points):
    return [min(math.dist(p, q) for j, q in enumerate(points) if j != i) for i, p in enumerate(points)]


def check_trunks(points):
    """The posadka's trunks in four rows 3 m apart and a nearest-neighbour median of 1-3 m (the spec's scenario). A row
    is a run of trunks whose sorted z steps less than half the spacing; its line is its median z, and the lines stand
    3 m apart within a trunk's jitter off its line."""
    zs = sorted(z for _, z in points)
    runs = [[zs[0]]]
    for a, b in zip(zs, zs[1:]):
        if b - a >= 1.5:
            runs.append([])
        runs[-1].append(b)
    lines = [statistics.median(run) for run in runs]
    assert len(lines) == 4, f"trunk rows {len(lines)}, not 4"
    for a, b in zip(lines, lines[1:]):
        assert abs(b - a - 3.0) <= gen.v("row_jitter_m"), f"rows at z {a:.3f} and {b:.3f}, not 3 m apart"
    assert 1.0 <= statistics.median(nearest(points)) <= 3.0


def trunks(belt):
    trees = {variant for _, variant, _ in gen.v("tree_mix")}
    return [(r["position_m"][0], r["position_m"][2]) for r in belt if r["variant"] in trees]


def test_the_posadkas_shape(data):
    """Its plants (trees and bushes) within a strip 10-30 m wide and at least 200 m long, its trunks in four rows 3 m
    apart with a nearest-neighbour median of 1-3 m, at least a fifth V01 or V13, and a gap at least 4 m wide across
    it."""
    belt = rows(data, group="posadka")
    xs = [r["position_m"][0] for r in belt]
    zs = [r["position_m"][2] for r in belt]
    assert 10.0 <= max(zs) - min(zs) <= 30.0
    assert max(xs) - min(xs) >= 200.0
    check_trunks(trunks(belt))
    assert min(nearest(list(zip(xs, zs)))) >= gen.v("plant_clearance_m") - MM
    unity = sum(r["asset"] in ("V01", "V13") for r in belt)
    assert unity / len(belt) >= 0.2
    ordered = sorted(xs)
    assert max(b - a for a, b in zip(ordered, ordered[1:])) >= 4.0


def test_a_belt_with_its_trunks_on_one_line_fails_the_trunk_check(data):
    """The known-wrong belt QA named: every trunk moved onto the centre line, the bushes kept, so the plants' strip is
    still 12.7 m wide. The trunk check must fail it."""
    on_one_line = [(x, gen.v("belt_z")) for x, _ in trunks(rows(data, group="posadka"))]
    with pytest.raises(AssertionError, match="trunk rows 1, not 4"):
        check_trunks(on_one_line)


def test_the_start_points(data, world):
    """At least two, one legs-on and one legs-off with the stand, each with a heading, at least 5 m in plan from any
    collision shape, wire or ground patch."""
    starts = data["start_points"]
    assert len(starts) >= 2
    assert [s for s in starts if s["legs"] == "on" and not s["stand"]]
    assert [s for s in starts if s["legs"] == "off" and s["stand"]]
    for start in starts:
        assert 0.0 <= start["heading_deg"] < 360.0 and start["position_m"][1] == 0.0
        clearance = gen.start_clearance(_loaded(world, data), start["position_m"])
        assert clearance >= 5.0, f"{start['id']}: {clearance:.2f} m"


def test_the_noclip_start_is_above_the_yard_looking_along_the_posadka(data):
    cam = data["noclip_start"]
    x, y, z = cam["position_m"]
    yx, yz = gen.v("yard_centre")
    hx, hz = gen.v("yard_half_m")
    assert abs(x - yx) <= hx + 10.0 and abs(z - yz) <= hz + 10.0 and y >= 15.0
    assert 0.0 < cam["pitch_deg"] < 45.0, "looking down at the yard and out to the belt"
    belt = rows(data, group="posadka")
    far = max(belt, key=lambda r: r["position_m"][0])["position_m"]
    near = min(belt, key=lambda r: r["position_m"][0])["position_m"]
    axis = math.degrees(math.atan2(far[0] - near[0], far[2] - near[2]))
    assert abs((cam["yaw_deg"] - axis + 180.0) % 360.0 - 180.0) <= 25.0, "along the belt's axis"
    to_far = math.degrees(math.atan2(far[0] - x, far[2] - z))
    assert abs((cam["yaw_deg"] - to_far + 180.0) % 360.0 - 180.0) <= 30.0, "the belt's far end ahead"
    assert data["review"]["views"][0] == cam, "number key 1 returns to the noclip start"


def test_the_ground_states_its_patches_and_holes(data, world):
    """Each ground patch's footprint is its bounds in the map; each hole band is its record carried to the map within
    1 mm; the floor is cut by each footprint and by the bands outside them; nothing else stands on a patch."""
    ground = data["ground"]
    assert ground["plane"] == {"y_m": 0.0, "surface": "belt_straw"}
    by_id = {r["id"]: r for r in data["instances"]}
    patch_ids = [r["id"] for r in data["instances"] if r["class"] in ("surfaces", "earthworks")]
    assert [p["instance"] for p in ground["patches"]] == patch_ids
    assert all(by_id[i].get("ground_patch") for i in patch_ids)
    for p in ground["patches"]:
        assert all(abs(a - b) <= MM for a, b in zip(p["footprint_m"], world.bounds_rect(by_id[p["instance"]])))

    holes = [(r["id"], h) for r in data["instances"] for h in world.assets.meta(r["asset"], r["variant"])["holes"]]
    assert [(h["instance"], h["record"]) for h in ground["holes"]] == [(i, h["id"]) for i, h in holes]
    assert {by_id[h["instance"]]["asset"] for h in ground["holes"]} == {"Z04", "E01"}
    for listed, (i, record) in zip(ground["holes"], holes):
        for a, b in zip(listed["centre_line_m"], record["centre_line"]):
            assert math.dist(a, gen.to_world(by_id[i], b)) <= MM
        assert listed["band_width_m"] == record["band_width_m"] and listed["depth_m"] == record["depth_m"]

    shaft = next(h for h in ground["holes"] if by_id[h["instance"]]["asset"] == "Z04")
    r = 0.5 * shaft["band_width_m"]
    line = shaft["centre_line_m"]
    band_rect = [min(p[0] for p in line) - r, min(p[2] for p in line) - r,
                 max(p[0] for p in line) + r, max(p[2] for p in line) + r]
    expected = [p["footprint_m"] for p in ground["patches"]] + [band_rect]
    assert len(ground["floor_cuts_m"]) == len(expected)
    for cut, rect in zip(ground["floor_cuts_m"], expected):
        assert all(abs(a - b) <= MM for a, b in zip(cut, rect))

    for p in ground["patches"]:
        for row in data["instances"]:
            if row["id"] == p["instance"] or row.get("ground_patch"):
                continue
            if row["asset"] in gen.PLANTS:
                x, z = row["position_m"][0], row["position_m"][2]
                assert gen.rect_distance(x, z, p["footprint_m"]) > 0.0, f"{row['variant']} #{row['id']} on a patch"
            else:
                rect = world.collision_rect(row)
                assert rect is None or not gen.overlaps(rect, p["footprint_m"]), f"{row['variant']} #{row['id']}"
    for a in ground["patches"]:
        for b in ground["patches"]:
            assert a is b or not gen.overlaps(a["footprint_m"], b["footprint_m"], -MM)


def test_every_span_hangs_on_its_ports(data, world):
    """Each conductor's ends lie within 5 mm of their ports' seats (a port plus one conductor radius along it), and
    no conductor leaves a port back across its pole by more than 5° (as the gallery's builder checks)."""
    by_id = {r["id"]: r for r in data["instances"]}
    for span in rows(data, asset="L03"):
        wires = world.assets.meta("L03", span["variant"])["wires"]
        hang = span["hang"]
        for end, (pole_id, ports) in enumerate(((hang["from"], hang["from_ports"]), (hang["to"], hang["to_ports"]))):
            pole = by_id[pole_id]
            meta = world.assets.meta(pole["asset"], pole["variant"])
            for wire, port_id in zip(wires, ports):
                port = next(p for p in meta["ports"] if p["id"] == port_id)
                seat = gen.to_world(pole, [port["position"][i] + port["direction"][i] * 0.5 * wire["diameter_m"]
                                           for i in range(3)])
                points = [gen.to_world(span, p) for p in wire["points"]]
                tip, other = (points[0], points[-1]) if end == 0 else (points[-1], points[0])
                assert math.dist(tip, seat) <= 0.005, f"#{span['id']} {port_id}"
                outward = gen.to_world(pole, port["direction"])
                outward = [outward[i] - pole["position_m"][i] for i in range(3)]
                chord = [other[i] - tip[i] for i in range(3)]
                along = sum(a * b for a, b in zip(chord, outward)) / math.dist(other, tip)
                assert along >= -math.sin(math.radians(5.0))


def _loaded(world, data):
    """A gen.Map holding the committed rows, for the generator's own clearance measure."""
    world.rows = data["instances"]
    return world
