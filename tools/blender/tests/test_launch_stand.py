"""generators.launch_stand (p5-drone task 1.4): D02 against the physics' runtime stand (LaunchStand.Build), the
spec's scenario "The drone rests on the stand" with D01's legs-off model on it, its LODs, solids, determinism, its
palette against its recipe and the catalog, and its metadata (mass_kg and steel)."""
import json
import math
from pathlib import Path

import pytest

from generators import drone, launch_stand
from lib import meta
from lib import params as params_lib

REPO = Path(__file__).resolve().parents[3]
VARIANT = "D02_launch_stand"
S = math.sqrt(0.5)


def load(variant):
    return json.loads((REPO / "assets-src" / "drone" / f"{variant}.json").read_text(encoding="utf-8"))


@pytest.fixture(scope="module")
def spec():
    return json.loads((REPO / "assets-src" / "drone" / "drone.json").read_text(encoding="utf-8"))


@pytest.fixture(scope="module")
def built():
    return launch_stand.build(load(VARIANT))


def num(x):
    return x["value"] if isinstance(x, dict) else x


def test_params_conform():
    assert params_lib.validate(launch_stand, load(VARIANT)) == []


def test_check_refuses_a_post_off_its_arm_and_the_lods_out_of_order(spec, tmp_path):
    s = json.loads(json.dumps(spec))
    s["stand"]["post_radius_m"]["value"] = 0.24  # past the arm's mount, beyond the motor
    path = tmp_path / "far.json"
    path.write_text(json.dumps(s), encoding="utf-8")
    problems = params_lib.validate(launch_stand, dict(load(VARIANT), spec=str(path)))
    assert any("lies off its arm" in problem for problem in problems)
    p = load(VARIANT)
    p["lod"]["lod2_switch_m"] = 2.0
    assert any("must increase" in problem for problem in params_lib.validate(launch_stand, p))


def test_collision_is_the_physics_runtime_stand(spec, built):
    """LaunchStand.Build at foot 0, yaw 0: shapes 0-3 a post under each motor in its order, the square tube's
    post_diameter_m across along body x and z, ground to post_height_m, centred post_radius_m out on the motor's
    diagonal; 4 and 5 the base bars along the diagonals, half-length post_radius_m + half the tube, on the ground."""
    st = spec["stand"]
    r, d, h = (num(st[k]) for k in ("post_radius_m", "post_diameter_m", "post_height_m"))
    shapes = built["collision"]
    assert len(shapes) == 6 and all(s["shape"] == "box" and s["material"] == st["material"] == "steel"
                                    for s in shapes)
    for c, motor in enumerate(spec["motors"]):
        m = num(motor["position_m"])
        post = shapes[c]
        assert post["center"] == pytest.approx([math.copysign(r * S, m[0]), h / 2, math.copysign(r * S, m[2])],
                                               abs=1e-6)
        assert post["size"] == pytest.approx([d, h, d]) and post["rotation"] == [0.0, 0.0, 0.0, 1.0]
        assert post["landable"]  # the arm rests on its top
    for bar, axis in zip(shapes[4:], ((S, 0.0, S), (S, 0.0, -S))):
        assert bar["center"] == pytest.approx([0.0, d / 2, 0.0]) and bar["size"] == pytest.approx([2 * (r + d / 2), d, d])
        x, y, z, w = bar["rotation"]
        local_x = (1 - 2 * (y * y + z * z), 2 * (x * y + w * z), 2 * (x * z - w * y))  # the quaternion's +x
        assert local_x == pytest.approx(axis, abs=1e-6) and not bar["landable"]


def test_the_drawn_stand_is_its_boxes(built):
    """Every vertex inside the shapes (the rounded corners inward of the boxes), and each box's extremes drawn:
    the posts' tops at post_height_m, the bars' ends and the ground."""
    lo, hi = built["lods"][0].bounds()
    shapes = built["collision"]
    assert hi[1] == pytest.approx(shapes[0]["size"][1], abs=1e-9) and lo[1] == pytest.approx(0.0, abs=1e-12)
    end = shapes[4]["size"][0] / 2
    assert hi[0] == pytest.approx(max(end * S + S * 0.01, 0.141421 + 0.01), abs=1e-6)
    for mesh in built["lods"]:
        for v in mesh.vertices:
            assert any(_inside(v, s, 1e-9) for s in shapes), v


def _inside(p, shape, tol):
    """Whether p lies inside a box shape, rotated about y only (the stand's)."""
    x, y, z, w = shape["rotation"]
    ax = (1 - 2 * (y * y + z * z), 0.0, 2 * (x * z - w * y))
    az = (2 * (x * z + w * y), 0.0, 1 - 2 * (x * x + y * y))
    d = [p[a] - shape["center"][a] for a in range(3)]
    local = (sum(d[a] * ax[a] for a in range(3)), d[1], sum(d[a] * az[a] for a in range(3)))
    return all(abs(local[a]) <= shape["size"][a] / 2 + tol for a in range(3))


def test_the_drone_rests_on_the_stand(spec, built):
    """The spec's scenario: the legs-off drone at LaunchStand.RestingOrigin (its arm plates' undersides, the arm
    contact shapes' radius below the arm plane, on the post tops): the arms touch all four posts within 1 mm, the
    coil clears the base by at least 20 mm, nothing of the drone enters the stand, and the stand's metadata carries
    its mass and steel."""
    st = spec["stand"]
    h, d = num(st["post_height_m"]), num(st["post_diameter_m"])
    arm_r = next(num(s["radius_m"]) for s in spec["contacts"]["shapes"]
                 if s["role"] == "body" and s["part"].startswith("arm_"))
    origin_y = h + arm_r
    params = load("D01_legs_off")
    layout = drone.Layout(drone.load_spec(params), params)
    parts = drone.part_meshes(params, layout, 0)
    posts = built["collision"][:4]
    for corner, post in zip(drone.CORNERS, posts):
        arm = layout.part(f"arm_{corner}")
        axis, r_in, _ = drone._arm_axis(arm)
        centre = (post["center"][0], post["center"][2])
        along = centre[0] * axis[0] + centre[1] * axis[2]
        across = abs(centre[0] * axis[2] - centre[1] * axis[0])
        assert r_in < along < r_in + arm["length_m"] and across < params["arm"]["width_m"] / 2, corner  # under it
        underside = min(v[1] for v in parts[f"arm_{corner}"].vertices) + origin_y  # the plate's flat underside
        assert 0.0 <= underside - h <= 0.001, corner  # the arm on the post's top
    drone_mesh = drone.build(params)["lods"][0]
    world = [(v[0], v[1] + origin_y, v[2]) for v in drone_mesh.vertices]
    for v in world:
        assert not any(_inside(v, s, -1e-6) for s in built["collision"]), v  # nothing inside the steel
    coil = [(v[0], v[1] + origin_y, v[2]) for name in ("coil", "coil_mount") for v in parts[name].vertices]
    clearance = min(v[1] for v in coil) - built["collision"][4]["size"][1]
    assert clearance >= 0.020  # the base bars cross right under it
    assert clearance == pytest.approx(0.135, abs=0.002)  # the sheet's fit: the front hoop 0.135 m over the bars
    data = meta.assemble("D02", VARIANT, "launch_stand", load(VARIANT), built, {"steel": "stand_steel"})
    assert data["mass_kg"] == num(st["mass_kg"]) == 1.6
    assert {m["physics"] for m in data["materials"]} == {"steel"}


def test_three_lods_lighter_each_and_the_same_size(built):
    tris = [m.triangles() for m in built["lods"]]
    assert len(tris) == 3 and tris == sorted(tris, reverse=True) and len(set(tris)) == 3 and tris[0] < 4000
    lo0, hi0 = built["lods"][0].bounds()
    for m in built["lods"][1:]:
        lo, hi = m.bounds()
        assert lo == pytest.approx(lo0, abs=1e-9) and hi == pytest.approx(hi0, abs=1e-9)
    assert built["switch_distances_m"] == [0.0, 3.0, 10.0] and built["cull_distance_m"] == 330.0
    assert built["cull_distance_m"] == load("D01_legs_off")["lod"]["cull_m"]  # culled with the drone on it


def test_tubes_are_closed_with_their_true_section(built):
    """LOD0's volume over the faces, each taken with its stated outward normal (lib.export winds by it): six square
    tubes of rounded corners, the bars counted where they overlap each other and the posts."""
    mesh = built["lods"][0]
    total = 0.0
    for indices, normal, _, _ in mesh.faces:
        pts = [mesh.vertices[i] for i in indices]
        n = [0.0, 0.0, 0.0]
        for a, b in zip(pts, pts[1:] + pts[:1]):
            n[0] += (a[1] - b[1]) * (a[2] + b[2])
            n[1] += (a[2] - b[2]) * (a[0] + b[0])
            n[2] += (a[0] - b[0]) * (a[1] + b[1])
        along = sum(n[a] * normal[a] for a in range(3))
        assert abs(along) > 0.99 * math.sqrt(sum(c * c for c in n)) * math.sqrt(sum(c * c for c in normal))  # planar
        centroid = [sum(p[a] for p in pts) / len(pts) for a in range(3)]
        total += 0.5 * math.copysign(1.0, along) * sum(centroid[a] * n[a] for a in range(3))
    r, side = 0.003, 0.02
    k = 3  # LOD0's corner segments: the polygon inscribed in each quarter circle
    section = side * side - 4 * r * r + 4 * (k / 2) * r * r * math.sin(math.pi / 2 / k)
    length = 4 * 0.25 + 2 * built["collision"][4]["size"][0]
    assert total / 3 == pytest.approx(section * length, rel=1e-9)


def test_wear_follows_the_seed_and_the_dial():
    a, b = launch_stand.build(load(VARIANT)), launch_stand.build(load(VARIANT))
    assert [m.vertices for m in a["lods"]] == [m.vertices for m in b["lods"]]
    assert [m.faces for m in a["lods"]] == [m.faces for m in b["lods"]]
    worn_uv = launch_stand.WORN[1]

    def worn_share(params):
        faces = launch_stand.build(params)["lods"][0].faces
        sides = [f for f in faces if len(f[0]) == 4]
        return sum(1 for f in sides if f[2][0] == worn_uv) / len(sides)

    none, some, most = (worn_share(dict(load(VARIANT), wear=w)) for w in (0.0, 0.3, 1.0))
    assert none == 0.0 < some < most  # only corner strips wear: 3 of each 4 side segments' 16
    assert most == pytest.approx(12 / 16)
    other = worn_share(dict(load(VARIANT), seed=2))
    assert other != some or launch_stand.build(dict(load(VARIANT), seed=2))["lods"][0].faces != a["lods"][0].faces


def test_palette_matches_its_recipe_and_the_catalog():
    catalog = json.loads((REPO / "assets-src" / "catalog.json").read_text(encoding="utf-8"))["assets"]["D02"]
    assert catalog["generator"] == "launch_stand" and catalog["variants"] == [VARIANT]
    assert set(catalog["materials"]) == set(launch_stand.PALETTES)
    for slot, columns in launch_stand.PALETTES.items():
        recipe = json.loads((REPO / "assets-src" / "materials" / f"{catalog['materials'][slot]}.json")
                            .read_text(encoding="utf-8"))
        assert recipe["kind"] == "palette" and tuple(recipe["colours"]) == columns


def test_metadata_validates(built):
    data = meta.assemble("D02", VARIANT, "launch_stand", load(VARIANT), built, {"steel": "stand_steel"})
    assert meta.validate(data, meta.load_physics_materials(REPO)) == []
    assert [s["kind"] for s in data["snag"]] == ["edge", "edge"]  # the base bars trip a dragged line or skid
    assert '"mass_kg": 1.6' in meta.dumps(data)
    assert meta.validate(dict(data, mass_kg=0.0), meta.load_physics_materials(REPO)) != []
