"""generators.vehicle: R01's clean base (p1-r01-vaz-sedan task 1.1) and its A and F kits (task 1.2): the published
dimensions in the metadata's terms (plan bounds, wheelbase, tracks, wheel cylinders, roof height), the collision
plans by group, the landable and material assignments, the "not built yet" refusals, the blank plates, the LOD
budget, determinism, params edits moving the geometry (AST-03); the A kit's holes by side and size, its flat corner's
drop and lean, its scatter's 1/r^2 density and its masks; the F kit's drop onto its rims, its bead wires, its burnt
slots and its soot patch. The collision audit itself runs in the Blender build (build.py), where it takes minutes."""
import copy
import json
import math

import pytest

from generators import vehicle
from lib import catalog, kits_vehicle as kits, masks, meta, params as params_lib

MM5 = 0.005


@pytest.fixture(scope="module")
def repo_root_module():
    from pathlib import Path
    return Path(__file__).resolve().parents[3]


def load(root, variant):
    entry = catalog.load(root)["assets"]["R01"]
    return catalog.load_params(catalog.params_path(root, entry, variant))


def set_key(params, key, value):
    *path, last = key.split(".")
    node = params
    for k in path:
        node = node[k]
    node[last] = value


@pytest.fixture(scope="module")
def r01_a(repo_root_module):
    return load(repo_root_module, "R01_A")


@pytest.fixture(scope="module")
def r01_f(repo_root_module):
    return load(repo_root_module, "R01_F")


@pytest.fixture(scope="module")
def r01(r01_a):
    """The clean base: R01_A's file with every kit item neutral (severity 0)."""
    params = copy.deepcopy(r01_a)
    for key, value in vehicle.NEUTRAL.items():
        set_key(params, key, value)
    return params


@pytest.fixture(scope="module")
def built(r01):
    return vehicle.build(r01)


@pytest.fixture(scope="module")
def built_a(r01_a):
    return vehicle.build(r01_a)


@pytest.fixture(scope="module")
def built_f(r01_f):
    return vehicle.build(r01_f)


def wheels(params):
    """The four wheel cylinders of the plan (the front wheelhouses' cylinders are rubber too)."""
    return [c for n, c in vehicle.collision_plan(vehicle.Body(params)) if n.startswith(("wheel_", "rim_"))]


def groups(plan):
    out = {}
    for name, _ in plan:
        group = vehicle.plan_group(name)
        out[group] = out.get(group, 0) + 1
    return out


# --- the clean base (task 1.1) --------------------------------------------------------------------------------------

def test_params_files_are_valid(r01, r01_a, r01_f):
    for params in (r01, r01_a, r01_f):
        assert params_lib.validate(vehicle, params) == []
    assert (r01["state"], r01["severity"], r01["plate"]) == ("A", 0.0, "blank")
    assert (r01_a["model"], r01_a["state"], r01_a["severity"], r01_a["plate"]) == ("R01", "A", 0.5, "blank")
    assert (r01_f["state"], r01_f["ride_drop"], r01_f["plate"]) == ("F", 0.17, "blank")


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


# The plan the audit's 2 cm zone round the roof, bonnet and boot lid asks of the clean base (design decision 7's
# note: the count is the audit's to set, not a budget).
PLAN_COUNTS = {"lower body": 15, "tops": 4, "shoulder rolls": 10, "greenhouse": 49, "wheels": 6, "bumpers": 10,
               "interior": 8, "rack": 8}


def test_collision_plan(r01):
    plan = vehicle.collision_plan(vehicle.Body(r01))
    names = [n for n, _ in plan]
    assert len(names) == len(set(names))
    assert groups(plan) == PLAN_COUNTS
    assert len(plan) == sum(PLAN_COUNTS.values()) == 110


def test_landable_and_materials(built, r01):
    plan = vehicle.collision_plan(vehicle.Body(r01))
    landable = sorted(n for n, c in plan if c["landable"])
    assert landable == ["bonnet", "boot_lid", "rack_tube_1", "rack_tube_3", "roof_front", "roof_mid", "roof_rear"]
    by_name = dict(plan)
    assert by_name["windscreen"]["material"] == "glass" and by_name["rear_screen"]["material"] == "glass"
    # QA 2.1's R3: the eight side panes are 5 mm glass boxes, the band's boxes set in behind them; per side the vent
    # in nine under the A pillar's slope, the front and rear windows in one, the quarter in two under the drip's fall
    panes = [n for n in by_name if n.startswith("band_pane_")]
    assert len(panes) == 2 * 13 and {by_name[n]["material"] for n in panes} == {"glass"}
    assert sorted({n.split("_")[2] for n in panes}) == ["0", "1", "2", "3"]
    assert all(by_name[n]["size"][1] == pytest.approx(vehicle.GLASS_T_M) for n in panes)
    assert {by_name[n]["material"] for n in by_name if n.startswith("seat_")} == {"fabric"}
    assert {by_name[n]["material"] for n in by_name if n.startswith(("bumper_", "rack_"))} == {"steel"}
    assert by_name["dashboard"]["material"] == "plastic"
    physics = {m["slot"]: m["physics"] for m in built["materials"]}
    assert physics["tyre"] == "rubber" and physics["seat"] == "fabric" and physics["paint"] == "sheet_metal"
    assert physics["plate"] == "sheet_metal"
    assert built["gaps"] == [] and built["holes"] == [] and built["textures"] == [] and built["micro"] == []
    assert [w["porosity"] for w in built["wind_volumes"]] == [0.0, 0.0]
    kinds = sorted({s["kind"] for s in built["snag"]})
    assert kinds == ["edge", "hook"]


@pytest.mark.parametrize("key, value, text", [
    ("state", "B", "'state' = 'B' is not built yet"),
    ("pose", "on_side", "'pose' = 'on_side' is not built yet"),
    ("front", "2105_2107", "'front' = '2105_2107' is not built yet"),
    ("model", "R02", "'model' = 'R02' is not built yet"),
    ("frag_holes", 16, "'severity' = 0 is the clean base, so it needs 'frag_holes' = 0"),
    ("bonnet", "corner_folded", "'bonnet' = 'closed'"),
    ("paint", "beige", "'paint' = 'beige' is not built yet"),
])
def test_not_built_yet_refusals(r01, key, value, text):
    params = copy.deepcopy(r01)
    set_key(params, key, value)
    problems = params_lib.validate(vehicle, params)
    assert any(text in p for p in problems), problems


@pytest.mark.parametrize("variant, key, value, text", [
    ("A", "burn", 0.5, "'burn' = 0.5 is the F kit's: state A needs 0"),
    ("A", "flat_tyres.rear_right", True, "more than one flat tyre is not built yet"),
    ("A", "glass_side.front_right", "crazed_shards", "'crazed_shards' is not built yet"),
    ("A", "burst_dir.azimuth", 160.0, "a struck rear corner) is not built yet"),
    ("A", "frag_hole_size", 0.1, "must stay under the large holes' 0.08 m"),
    ("A", "bonnet", "standing", "'bonnet' = 'standing' is not built yet in state A"),
    ("A", "doors.front_left", "open", "'doors.front_left' = 'open' is not built yet"),
    ("A", "frag_holes", 60, "no room on the burst side's panels"),
    ("F", "glass_side.rear_left", "intact", "must be 'gone' in state F"),
    ("F", "ride_drop", 0.05, "is under the tyre's own loss"),
    ("F", "frag_holes", 5, "'frag_holes' = 5 is not built yet in state F"),
    ("F", "rims_melted", True, "'rims_melted' = True is not built yet"),
    ("F", "burn_origin", "cargo", "'burn_origin' = 'cargo' is not built yet in state F"),
])
def test_kit_refusals(r01_a, r01_f, variant, key, value, text):
    params = copy.deepcopy(r01_a if variant == "A" else r01_f)
    set_key(params, key, value)
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


@pytest.mark.parametrize("which", ["built", "built_a", "built_f"])
def test_lod_budget(which, request):
    """AST-08: LOD0 with the micro meshes it renders (QA 2.1's R2) under 60 000 triangles, each further LOD at most
    half the previous."""
    result = request.getfixturevalue(which)
    tris = [m.triangles() for m in result["lods"]]
    assert tris[0] + sum(m["mesh"].triangles() for m in result["micro"] if m["lod"] == 0) < 60000
    assert all(b * 2 <= a for a, b in zip(tris, tris[1:])), tris
    assert result["switch_distances_m"] == [0.0, 20.0, 80.0, 300.0] and result["cull_distance_m"] == 1500.0
    assert result["thin_wire"] == [False] * 4


@pytest.mark.parametrize("which", ["r01", "r01_a", "r01_f"])
def test_deterministic(which, request):
    params = request.getfixturevalue(which)
    one, two = vehicle.build(params), vehicle.build(params)
    assert one["lods"][0].vertices == two["lods"][0].vertices and one["collision"] == two["collision"]
    assert [t["png"] for t in one["textures"]] == [t["png"] for t in two["textures"]]


def test_params_edits_move_the_geometry(r01):
    lower = copy.deepcopy(r01)
    lower["height"] = 1.42
    lower["roof_rack"] = False
    assert vehicle.build(lower)["lods"][0].bounds()[1][1] <= 1.42 + 1e-9
    wider = copy.deepcopy(r01)
    wider["track_rear"] = 1.32
    w = [c for c in wheels(wider) if c["center"][0] > 0]
    assert abs(w[0]["center"][2] - w[1]["center"][2]) == pytest.approx(1.32, abs=1e-9)


# --- the A kit (letter M) ---------------------------------------------------------------------------------------------

def test_a_holes_by_side_and_size(r01_a, built_a):
    """The sheet's M: about 6 large holes (0.08-0.25 m, the struck corner's lamp tear one of them) and 10 small
    (10-30 mm), all on the burst side (the left, the burst from 290 degrees); 30 mm or more cut in the mesh."""
    kit = vehicle.Kit(vehicle.Body(r01_a), r01_a)
    assert kit.side == -1 and kit.notch is not None
    assert {h.side for h in kit.holes} == {-1}
    large = [h for h in kit.holes if h.d >= kits.LARGE_MIN_M]
    small = [h for h in kit.holes if h.d < kits.CUT_MIN_M]
    assert len(kit.holes) + 1 == r01_a["frag_holes"] == 16  # the lamp tear is the sixteenth
    assert len(large) + 1 == round(16 * 6 / 16) == 6 and len(small) == 10
    assert all(kits.LARGE_MIN_M <= h.d <= kits.LARGE_MAX_M and h.cut and len(h.petals) >= 5 for h in large)
    assert all(kits.SMALL_MIN_M <= h.d < r01_a["frag_hole_size"] and not h.cut for h in small)
    # each cut hole's rim is a closed edge snag, its petals' torn outlines too; the far side carries none
    rims = [s for s in built_a["snag"] if s["kind"] == "edge" and s["points"][0] == s["points"][-1]
            and len(s["points"]) == 49]
    assert len(rims) == len(large)
    assert all(p[2] < 0 for s in rims for p in s["points"])


def test_a_petals_reach_the_open_centre(r01_a):
    """A large hole stays closed in collision (the sheet's deviation for holes of 0.25 m or less): its petals,
    bent 10-30 mm in (measured M), reach to within AST-05's 5 cm of the open centre."""
    kit = vehicle.Kit(vehicle.Body(r01_a), r01_a)
    for h in kit.holes:
        for petal in h.petals:
            flat, bend = h.petal_tip(petal)
            rim = h.rim_radius(petal["tip"])
            length = (rim - flat)
            depth = length * math.sin(bend)
            reach = rim - length * math.cos(bend)
            assert 0.0 < depth <= kits.PETAL_DEPTH_M[1] + 1e-9
            assert math.hypot(reach, depth) <= kits.APERTURE_M + 1e-9


def test_a_flat_corner_drop_and_lean(r01_a, built_a):
    """The flat front tyre (measured M): its hub 0.04-0.08 m lower, the body settled on its four springs (QA 2.1's
    R1): down a quarter of the drop at its centre, its corners -3/4, -1/4, -1/4 and +1/4 of it (the flat one first,
    the opposite one last; the tracks differ, so within 1 mm), leaning 1-2 degrees across and tilting 1-2 in all."""
    body = vehicle.Body(r01_a)
    kit = vehicle.Kit(body, r01_a)
    drop = body.z_wc - kit.hubs["front_left"][1]
    assert 0.04 <= drop <= 0.08 and drop == pytest.approx(kits.FLAT_DROP_M, abs=1e-6)
    for corner in ("front_right", "rear_left", "rear_right"):
        assert kit.hubs[corner][1] == pytest.approx(body.z_wc, abs=1e-9)
    settled = vehicle._settle(body, kit)
    want = {"front_left": -0.75, "front_right": -0.25, "rear_left": -0.25, "rear_right": 0.25, "centre": -0.25}
    for corner, share in want.items():
        assert settled[corner] == pytest.approx(share * drop, abs=0.001), corner
    assert 1.0 <= settled["across"] <= 2.0 and 1.0 <= settled["tilt"] <= 2.0
    assert settled["along"] == pytest.approx(math.degrees(math.atan(drop / 2 / r01_a["wheelbase"])), abs=0.02)
    print(f"R01_A settled: across {settled['across']:.2f}, along {settled['along']:.2f}, tilt {settled['tilt']:.2f} deg")
    # the flat wheel's cylinder: over its dropped hub, from the tread flattened on the ground up to the published
    # 0.594 m tyre's top, 0.165 m wide
    flat = dict(named(r01_a))["wheel_front_left"]
    hub = kit.hubs["front_left"]
    assert 2 * body.r_tyre == pytest.approx(0.594, abs=0.0005)
    assert flat["center"][1] - flat["radius"] == pytest.approx(0.0, abs=1e-6)
    assert flat["center"][1] + flat["radius"] == pytest.approx(hub[1] + body.r_tyre, abs=1e-6)
    assert (flat["center"][0], flat["center"][2]) == pytest.approx((hub[0], hub[2]), abs=1e-6)
    assert flat["height"] == pytest.approx(0.165, abs=0.0005)


def test_a_published_dimensions(r01_a, built_a):
    """The scenario "Published dimensions" on R01_A's metadata: the body's bounds without its scatter (bounds.lod,
    the LOD group's frame, QA 2.1's S2) 4.073 x 1.611 m in plan within 2 %, and its roof 1.40-1.44 m over the ground
    once settled on its flat corner (QA 2.1's R1; the rack's rails stand 0.10 m over it)."""
    data = meta.assemble("R01", "R01_A", "vehicle", r01_a, built_a, {m["slot"]: "x" for m in built_a["materials"]})
    lod, full = data["bounds"]["lod"], data["bounds"]
    assert lod["max"][0] - lod["min"][0] == pytest.approx(4.073, rel=0.02)
    assert lod["max"][2] - lod["min"][2] == pytest.approx(1.611, rel=0.02)
    assert full["max"][0] - full["min"][0] > 6.0  # the scatter's, which the LOD group no longer measures from
    roof = vehicle.build(dict(r01_a, roof_rack=False))["lod_bounds"][1][1]
    assert 1.40 <= roof <= 1.44
    print(f"R01_A roof {roof:.4f} m, body {lod['max'][0] - lod['min'][0]:.3f} x {lod['max'][2] - lod['min'][2]:.3f} m")


def named(params):
    """The kit's collision plan with its names (build's "collision" is its shapes, in order)."""
    body = vehicle.Body(params)
    kit = vehicle.Kit(body, params)
    return vehicle.collision_plan(body, kit, vehicle._pieces(body, kit))


def test_a_scatter_density(r01_a):
    """K07's scatter round the struck corner (the sheet: 1-4 m, densest within 2 m, 1/r^2): a lamp bowl, a bezel
    ring and shards of trim, at distances log-uniform between 1 m and debris_radius, none inside the car."""
    # 80 pieces for the statistics, over 8 m: so many do not fit apart within the sheet's 4 m
    params = copy.deepcopy(r01_a)
    params["debris_count"], params["debris_radius"] = 80, 8.0
    params["seed"] = 186  # the statistics' own seed, whose 80 pieces fit apart (R01_A's seed is set by M's holes)
    body = vehicle.Body(params)
    kit = vehicle.Kit(body, params)
    pieces = vehicle._pieces(body, kit)
    corner = (body.x(0.0), kit.side * body.hw(body.s_front))
    r = [math.hypot(p["rigid"].t[0] - corner[0], p["rigid"].t[2] - corner[1]) for p in pieces]
    assert len(pieces) == 80 and all(1.0 - 1e-9 <= d <= 8.0 + 1e-9 for d in r)
    # 1/r^2 areal density: ln r uniform between ln 1 and ln 8 (mean ln sqrt 8, half within sqrt 8 m)
    # at 3 sigma (QA 2.1's S3: at n = 80 the mean of ln r has sigma 0.067, the share 0.056); uniform areal density
    # (mean ln r 1.61) and uniform in r (1.38) still fail
    assert sum(math.log(d) for d in r) / len(r) == pytest.approx(math.log(8.0) / 2, abs=0.20)
    assert sum(d < math.sqrt(8.0) for d in r) / len(r) == pytest.approx(0.5, abs=0.17)
    for p in pieces:
        x, z = p["rigid"].t[0], p["rigid"].t[2]
        assert abs(x) > body.L / 2 or abs(z) > body.W / 2
    a_pieces = vehicle._pieces(vehicle.Body(r01_a), vehicle.Kit(vehicle.Body(r01_a), r01_a))
    kinds = [p["collision"][0]["shape"] for p in a_pieces]
    assert len(a_pieces) == 12 and kinds.count("sphere") == 1 and kinds[1] == "capsule" and kinds.count("box") == 10


def test_a_glass_and_cabin(r01_a, built_a):
    """Side glass gone on the burst side (crumbs inside and under the doors), the far side's in, the crazed
    windscreen its own slot over the transparent glass, the rear screen in; the greenhouse's porosity 0.35."""
    lod0 = built_a["lods"][0]
    rear_screen_x = vehicle.Body(r01_a).x(vehicle.Body(r01_a).s_rs_top)
    side_glass = [f for f in lod0.faces if f[3] == "glass" and abs(f[1][2]) > 0.5  # facing out, ahead of the screen
                  and all(lod0.vertices[i][0] < rear_screen_x for i in f[0])]
    left_glass = [f for f in side_glass if lod0.vertices[f[0][0]][2] < -0.4]
    right_glass = [f for f in side_glass if lod0.vertices[f[0][0]][2] > 0.4]
    assert not left_glass and right_glass
    assert "windscreen" in lod0.slots() and "windscreen" in built_a["lods"][1].slots()
    assert "windscreen" not in built_a["lods"][2].slots()
    # the panes with the cabin behind them are the transparent glass, the far hulls' glass the opaque (task 1.4)
    assert "glass" in built_a["lods"][1].slots() and "glass_hull" not in built_a["lods"][1].slots()
    assert {"glass_hull"} <= set(built_a["lods"][2].slots()) and "glass" not in built_a["lods"][2].slots()
    crumbs = [m for m in built_a["micro"] if m["kind"] == "glass_crumbs"]
    assert len(crumbs) == 1 and crumbs[0]["count"] == 2 * vehicle.CRUMBS_PER_WINDOW
    # M (QA 2.1's R2): the crumbs inside on the seats and the floor, a few scattered on the ground, never a carpet:
    # each crumb five faces (no bottom), the ground's lowest at 0.5 mm
    mesh = crumbs[0]["mesh"]
    lows = [min(mesh.vertices[i][1] for f in mesh.faces[k:k + 5] for i in f[0]) for k in range(0, len(mesh.faces), 5)]
    ground = [k for k, y in enumerate(lows) if y < 0.01]
    assert len(lows) == crumbs[0]["count"]
    assert len(ground) == 2 * round(vehicle.CRUMBS_PER_WINDOW * (1 - vehicle.CRUMB_INSIDE))
    out = [abs(mesh.vertices[mesh.faces[5 * k][0][0]][2]) - 0.8 for k in ground]
    assert max(out) > 2.0 and sum(out) / len(out) > 1.0  # spread to 3 m from the sill, not banked under the doors
    assert [w["porosity"] for w in built_a["wind_volumes"]] == [0.0, 0.35]
    plan = dict(named(r01_a))
    assert [c for _, c in named(r01_a)] == built_a["collision"]
    for screen, count in (("windscreen", 6), ("rear_screen", 3)):  # the crazed pane in two across, the other in one
        panes = [n for n in plan if n.startswith(screen + "_") and not n.startswith(screen + "_frame")]
        frames = [n for n in plan if n.startswith(screen + "_frame")]
        assert len(panes) == count and {plan[n]["material"] for n in panes} == {"glass"}
        assert len(frames) == 4 and {plan[n]["material"] for n in frames} == {"sheet_metal"}
    assert not any(n.startswith("cabin_") for n in plan)
    # QA 2.1's R3: the far side's panes in as 5 mm glass boxes, the burst side's none
    far = [n for n in plan if n.startswith("band_pane_") and n.endswith("_right")]
    assert len(far) == 13 and {plan[n]["material"] for n in far} == {"glass"}
    assert not any(n.startswith("band_pane_") and n.endswith("_left") for n in plan)


def test_a_landable_and_materials(r01_a):
    plan = named(r01_a)
    landable = sorted(n for n, c in plan if c["landable"])
    assert landable == ["bonnet_front", "bonnet_side", "bonnet_tear", "boot_lid", "rack_tube_1", "rack_tube_3",
                        "roof_0_left", "roof_0_right", "roof_1_left", "roof_1_right", "roof_2_left", "roof_2_right"]
    by_name = dict(plan)
    assert not any(by_name[n]["landable"] for n in by_name if n.startswith(("flap_", "scuttle_")))
    assert {by_name[n]["material"] for n in by_name if n.startswith("wheel_")} == {"rubber"}
    assert {by_name[n]["material"] for n in by_name if n.startswith("seat_")} == {"fabric"}
    assert {by_name[n]["material"] for n in by_name if n.startswith("scatter_")} == {"steel"}


def test_a_textures(r01_a, built_a):
    """The crack mask (1k on its long side) over the crazed pane's UVs, its lines drawn; the damage mask over the
    paint's canvas (2048 x 1024, RGBA): halos in R, dust in B, no soot or burn in A."""
    by_role = {t["role"]: t for t in built_a["textures"]}
    crack, damage = by_role["crack_mask"], by_role["damage_mask"]
    assert crack["slot"] == "windscreen" and damage["slot"] == "paint"
    w, h, channels, planes = masks.read_png(crack["png"])
    assert (w, h, channels) == (512, 1024, 1)
    lines = sum(1 for v in planes[0] if v == 255) / (w * h)
    assert 0.005 < lines < 0.3
    # under the lines, the haze of the finer cracks over the whole pane (task 1.4: M's pane is crazed across)
    haze = 255 * vehicle.CRACK_HAZE
    assert all(v >= int(haze * vehicle.CRACK_HAZE_FLOOR) for v in planes[0])
    assert max(v for v in planes[0] if v < 255) == pytest.approx(haze, abs=2)  # at the impacts (under a line there)
    body = vehicle.Body(r01_a)
    assert tuple(crack["uv_min"]) == pytest.approx(vehicle._pane_extent(body)[0])
    assert tuple(crack["uv_max"]) == pytest.approx(vehicle._pane_extent(body)[1])
    w, h, channels, planes = masks.read_png(damage["png"])
    assert (w, h, channels) == (2048, 1024, 4)
    assert max(planes[0]) > 0 and max(planes[2]) > 0 and max(planes[1]) == 0 and max(planes[3]) == 0
    data = meta.assemble("R01", "R01_A", "vehicle", r01_a, built_a, {m["slot"]: "x" for m in built_a["materials"]})
    assert sorted(t["file"] for t in data["textures"]) == ["R01_A_damage.png", "R01_A_windscreen_cracks.png"]


# --- the F kit ---------------------------------------------------------------------------------------------------------

def test_f_drop_and_rims(r01_f, built_f):
    """On its steel rims (the sheet's F drop 0.17 m, 0.15-0.20): the body 0.17 m down, each wheel a steel cylinder at
    the rim's flange radius on the ground, two bead-wire rings per wheel, the roof sagging 0.04 m with its crease."""
    body = vehicle.Body(r01_f)
    kit = vehicle.Kit(body, r01_f)
    assert kit.T.t == pytest.approx((0.0, -0.17, 0.0), abs=MM5) and kit.T.is_identity() is False
    assert 0.15 <= -kit.T.t[1] <= 0.20
    plan = named(r01_f)
    assert [c for _, c in plan] == built_f["collision"]
    rims = [c for n, c in plan if n.startswith("rim_")]
    assert len(rims) == 4 and not any(n.startswith(("wheel_", "wheelhouse_")) for n, _ in plan)
    for c in rims:
        assert c["radius"] == pytest.approx(vehicle.RIM_FLANGE_R_M) and c["material"] == "steel"
        assert c["center"][1] == pytest.approx(vehicle.RIM_FLANGE_R_M)
    beads = [n for n, _ in plan if n.startswith("bead_")]
    assert len(beads) == 8 * 8  # eight rings of K07's eight capsules
    crown = body.cap_section(body.s_crease, 0.0)[1]
    flat = vehicle.Body(dict(r01_f, roof_push=0.0)).cap_section(body.s_crease, 0.0)[1]
    assert flat - crown == pytest.approx(0.04, abs=1e-6)
    lowest = min(v[1] for v in built_f["lods"][0].vertices if abs(v[0]) < 1.0 and abs(v[2]) < 0.4)
    assert lowest == pytest.approx(min(y for _, y in body.underbody) - 0.17, abs=0.002)


def test_f_burnt_surfaces(r01_f, built_f):
    """Soot-black paint, bare steel over the engine bay and the roof (the damage mask's A on the one soot slot, task
    1.4), dulled chrome, no glass, rubber or lenses; the seats as frames with springs (hooks); the soot patch a micro
    mesh in LOD0-LOD2; porosity 0.55."""
    lod0 = built_f["lods"][0]
    slots = lod0.slots()
    assert {"soot", "chrome_dull", "rack", "wire"} <= set(slots)
    assert not {"paint", "bare", "chrome", "glass", "windscreen", "tyre", "lens", "seat", "vinyl", "plate"} & set(slots)
    assert [m["lod"] for m in built_f["micro"] if m["kind"] == "soot_patch"] == [0, 1, 2]
    assert [w["porosity"] for w in built_f["wind_volumes"]] == [0.0, 0.55]
    hooks = [s for s in built_f["snag"] if s["kind"] == "hook"]
    assert len([s for s in hooks if s["radius_m"] == vehicle.SPRING_R_M]) >= 6 * 3
    by_role = {t["role"]: t for t in built_f["textures"]}
    assert by_role["damage_mask"]["slot"] == "soot"
    width, height, _, planes = masks.read_png(by_role["damage_mask"]["png"])
    assert min(planes[1]) == 255 and 0 < sum(1 for v in planes[3] if v) < len(planes[3])
    body = vehicle.Body(r01_f)
    (u0, v0), (u1, v1) = vehicle.CANVAS_MIN, vehicle.CANVAS_MAX
    du, dv = vehicle.CANVAS["top"]

    def burnt_off(s):  # the top group's texel on the centre line at station s
        x = int((body.x(s) + du - u0) / (u1 - u0) * width)
        y = int((v1 - dv) / (v1 - v0) * height)
        return planes[3][y * width + x]
    assert burnt_off(body.axle[0] + 0.15) == 255  # the bonnet over the engine bay
    assert burnt_off((body.st["roof_front"] + body.st["roof_rear"]) / 2) == 255  # the cabin roof
    assert burnt_off(body.s_rear - 0.1) == 0  # the boot lid stays sooted


def test_k07_pieces_match_their_params_files(repo_root_module):
    entry = catalog.load(repo_root_module)["assets"]["K07"]
    for variant in entry["variants"]:
        params = catalog.load_params(catalog.params_path(repo_root_module, entry, variant))
        sizes = {k: params[k] for k in ("diameter_m", "section_m", "length_m", "width_m", "bend_m")}
        assert vehicle.K07[params["piece"]] == sizes, variant


# The plans the audit's 2 cm zone asks of the A and F cars (the counts are its to set, not a budget; pinned by group
# so a change is seen). The open cabin's door tops, trims, wheel tubs and boot; A's torn bonnet edge, its folded
# flap's strips, the crazed pane in six and the K07 scatter; F's open front arches, frames and bead wires.
A_COUNTS = {"lower body": 39, "tops": 11, "shoulder rolls": 13, "greenhouse": 74, "bumpers": 10, "interior": 19,
            "rack": 8, "flap": 13, "wheels": 6, "scatter": 19}
F_COUNTS = {"lower body": 53, "tops": 5, "shoulder rolls": 8, "greenhouse": 54, "bumpers": 10, "interior": 18,
            "rack": 8, "wheels": 4, "bead wires": 64}


@pytest.mark.parametrize("which, counts", [("r01_a", "A"), ("r01_f", "F")])
def test_kit_plans_by_group(which, counts, request):
    plan = named(request.getfixturevalue(which))
    pinned = A_COUNTS if counts == "A" else F_COUNTS
    assert len({n for n, _ in plan}) == len(plan)
    assert groups(plan) == pinned
    assert len(plan) == sum(pinned.values())
