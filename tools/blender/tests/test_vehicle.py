"""generators.vehicle, the clean base of R01 (p1-r01-vaz-sedan task 1.1): the published dimensions in the metadata's
terms (plan bounds, wheelbase, tracks, wheel cylinders, roof height), the collision plan, the landable and material
assignments, the "not built yet" refusals, the blank plates, the LOD budget, determinism, and params edits moving the
geometry (AST-03). The collision audit itself runs in the Blender build (build.py), where it takes minutes."""
import copy
import json
import math

import pytest

from generators import vehicle
from lib import catalog, meta, params as params_lib

MM5 = 0.005


@pytest.fixture(scope="module")
def r01(repo_root_module):
    entry = catalog.load(repo_root_module)["assets"]["R01"]
    return catalog.load_params(catalog.params_path(repo_root_module, entry, "R01_A"))


@pytest.fixture(scope="module")
def repo_root_module():
    from pathlib import Path
    return Path(__file__).resolve().parents[3]


@pytest.fixture(scope="module")
def built(r01):
    return vehicle.build(r01)


def wheels(params):
    """The four wheel cylinders of the plan (the front wheelhouses' cylinders are rubber too)."""
    return [c for n, c in vehicle.collision_plan(vehicle.Body(params)) if n.startswith("wheel_")]


def test_params_file_is_valid_and_the_clean_base(r01):
    assert params_lib.validate(vehicle, r01) == []
    assert (r01["model"], r01["state"], r01["severity"], r01["plate"]) == ("R01", "A", 0.0, "blank")


def test_published_dimensions(r01, built):
    lo, hi = built["lods"][0].bounds()
    assert hi[0] - lo[0] == pytest.approx(4.073, rel=0.02)
    assert hi[2] - lo[2] == pytest.approx(1.611, rel=0.02)
    assert hi[0] - lo[0] == pytest.approx(4.073, abs=0.001)  # tip to tip over the overriders
    w = wheels(r01)
    assert len(w) == 4
    xs = sorted({round(c["center"][0], 6) for c in w})
    assert xs[1] - xs[0] == pytest.approx(2.424, abs=MM5)
    for x, track in zip(xs, (1.349, 1.305)):
        zs = sorted(c["center"][2] for c in w if round(c["center"][0], 6) == x)
        assert zs[1] - zs[0] == pytest.approx(track, abs=MM5)
    for c in w:
        assert (2 * c["radius"], c["height"]) == pytest.approx((0.594, 0.165), abs=0.0005)
        assert c["rotation"] == pytest.approx(meta.rotation_from_y((0.0, 0.0, 1.0)))  # axle across the car
    # the roof: the body without its rack stands 1.40-1.44 m; the rack's rails 0.10 m over it
    bare = vehicle.build(dict(r01, roof_rack=False))["lods"][0].bounds()
    assert 1.40 <= bare[1][1] <= 1.44 + 1e-9
    assert hi[1] == pytest.approx(1.44 + vehicle.RACK_RISE_M + vehicle.RACK_R_M, abs=0.002)


def test_collision_plan(r01):
    plan = vehicle.collision_plan(vehicle.Body(r01))
    names = [n for n, _ in plan]
    assert len(names) == len(set(names))
    groups = {}
    for name, _ in plan:
        key = next(g for g in PLAN_GROUPS if name.startswith(PLAN_GROUPS[g]))
        groups[key] = groups.get(key, 0) + 1
    assert groups == PLAN_COUNTS
    assert len(plan) == sum(PLAN_COUNTS.values())


PLAN_GROUPS = {"lower body": ("nose_", "lower_mid", "arch_", "tail_"),
               "tops": ("front_wedge", "bonnet", "rear_wedge", "boot_lid"), "shoulder rolls": ("roll_",),
               "greenhouse": ("cabin_", "band_", "windscreen", "rear_screen", "roof_", "pillar_"),
               "wheels": ("wheel_", "wheelhouse_"), "bumpers": ("bumper_", "overrider_"),
               "interior": ("seat_", "dashboard", "steering"), "rack": ("rack_",)}
# The plan the audit's 2 cm zone round the roof, bonnet and boot lid asks of this shape (generators.vehicle,
# collision_plan): design decision 7's 28 is not reachable with the audit passing (task 1.1's as-built note).
PLAN_COUNTS = {"lower body": 15, "tops": 4, "shoulder rolls": 10, "greenhouse": 23, "wheels": 6, "bumpers": 10,
               "interior": 8, "rack": 8}


def test_landable_and_materials(built, r01):
    plan = vehicle.collision_plan(vehicle.Body(r01))
    landable = sorted(n for n, c in plan if c["landable"])
    assert landable == ["bonnet", "boot_lid", "rack_tube_1", "rack_tube_3", "roof_front", "roof_mid", "roof_rear"]
    by_name = dict(plan)
    assert by_name["windscreen"]["material"] == "glass" and by_name["rear_screen"]["material"] == "glass"
    assert {by_name[n]["material"] for n in by_name if n.startswith("seat_")} == {"fabric"}
    assert {by_name[n]["material"] for n in by_name if n.startswith(("bumper_", "rack_"))} == {"steel"}
    assert by_name["dashboard"]["material"] == "plastic"
    physics = {m["slot"]: m["physics"] for m in built["materials"]}
    assert physics["tyre"] == "rubber" and physics["seat"] == "fabric" and physics["paint"] == "sheet_metal"
    assert physics["plate"] == "sheet_metal"
    assert built["gaps"] == [] and built["holes"] == []
    assert [w["porosity"] for w in built["wind_volumes"]] == [0.0, 0.0]
    kinds = sorted({s["kind"] for s in built["snag"]})
    assert kinds == ["edge", "hook"]


@pytest.mark.parametrize("key, value, text", [
    ("state", "B", "'state' = 'B' is not built yet"),
    ("state", "F", "'state' = 'F' is not built yet: the F kit is task 1.2's"),
    ("pose", "on_side", "'pose' = 'on_side' is not built yet"),
    ("front", "2105_2107", "'front' = '2105_2107' is not built yet"),
    ("model", "R02", "'model' = 'R02' is not built yet"),
    ("severity", 0.5, "not built yet: the A kit and the damage mask are task 1.2's"),
    ("frag_holes", 16, "'frag_holes' = 0"),
    ("bonnet", "corner_folded", "'bonnet' = 'closed'"),
    ("paint", "beige", "'paint' = 'beige' is not built yet"),
])
def test_not_built_yet_refusals(r01, key, value, text):
    params = copy.deepcopy(r01)
    params[key] = value
    problems = params_lib.validate(vehicle, params)
    assert any(text in p for p in problems), problems


def plate_boxes(mesh):
    """The plate slot's boxes as (min, max) bounds, front to rear (six faces each)."""
    faces = [f for f in mesh.faces if f[3] == "plate"]
    boxes = []
    for k in range(0, len(faces), 6):
        pts = [mesh.vertices[i] for f in faces[k:k + 6] for i in f[0]]
        boxes.append((tuple(min(p[a] for p in pts) for a in range(3)), tuple(max(p[a] for p in pts) for a in range(3))))
    return sorted(boxes)


def test_blank_plates(r01, built):
    """SEC-02: the clean base carries two blank plates of the standard 520 x 112 mm, on the front bumper's middle and
    the rear panel, inside the overall length; `plate` none carries none; LOD2 and LOD3 drop them."""
    boxes = plate_boxes(built["lods"][0])
    assert len(boxes) == 2 and len(plate_boxes(built["lods"][1])) == 2
    assert all("plate" not in m.slots() for m in built["lods"][2:])
    lo, hi = built["lods"][0].bounds()
    (f_lo, f_hi), (r_lo, r_hi) = boxes
    for b_lo, b_hi in boxes:
        assert (b_hi[2] - b_lo[2], b_hi[1] - b_lo[1]) == pytest.approx(vehicle.PLATE_M, abs=1e-9)
        assert b_hi[0] - b_lo[0] == pytest.approx(vehicle.PLATE_T_M, abs=1e-9)
        assert b_lo[2] == pytest.approx(-b_hi[2])  # on the centre line
    front_top, rear_bottom = vehicle.PRESETS["R01"]["plates"]
    assert lo[0] < f_lo[0] and f_hi[1] == pytest.approx(front_top)  # behind the overriders' faces
    assert r_hi[0] < hi[0] and r_lo[1] == pytest.approx(rear_bottom)
    assert all(f[3] != "plate" for f in vehicle.build(dict(r01, plate="none"))["lods"][0].faces)


def test_dimension_checks(r01):
    params = copy.deepcopy(r01)
    params["wheelbase"] = 2.5
    problems = params_lib.validate(vehicle, params)
    assert any("'wheelbase' = 2.5 is more than 2% off the model's published 2.424" in p for p in problems)
    assert any("must make 'length' 4.073 within 1 mm" in p for p in problems)


def test_lod_budget(built):
    tris = [m.triangles() for m in built["lods"]]
    assert tris[0] < 60000
    assert all(b * 2 <= a for a, b in zip(tris, tris[1:])), tris
    assert built["switch_distances_m"] == [0.0, 20.0, 80.0, 300.0] and built["cull_distance_m"] == 1500.0
    assert built["thin_wire"] == [False] * 4


def test_deterministic(r01, built):
    again = vehicle.build(r01)
    assert again["lods"][0].vertices == built["lods"][0].vertices and again["collision"] == built["collision"]


def test_params_edits_move_the_geometry(r01):
    lower = copy.deepcopy(r01)
    lower["height"] = 1.42
    lower["roof_rack"] = False
    assert vehicle.build(lower)["lods"][0].bounds()[1][1] <= 1.42 + 1e-9
    wider = copy.deepcopy(r01)
    wider["track_rear"] = 1.32
    w = [c for c in wheels(wider) if c["center"][0] > 0]
    assert abs(w[0]["center"][2] - w[1]["center"][2]) == pytest.approx(1.32, abs=1e-9)
