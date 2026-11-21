"""generators.track_kit (w20-hunter-track task 1.3): every drawn kind of the layout has its variant, each variant's
drawn size is its params' rows, each gate's opening is the rows' clear space with nothing in it, the metadata validates
and its collision passes the build's audit, and a ribbon follows its layout row."""
import json
import math
from pathlib import Path

import pytest

from generators import track_kit
from lib import audit, meta
from lib import params as params_lib

REPO = Path(__file__).resolve().parents[3]
CATALOG = json.loads((REPO / "assets-src" / "catalog.json").read_text(encoding="utf-8"))["assets"]["T01"]
LAYOUT = json.loads((REPO / "maps" / "hunter-09" / "layout.json").read_text(encoding="utf-8"))["objects"]
VARIANTS = CATALOG["variants"]
GATES = {"T01_frame_8x10": 1, "T01_frame_square_a": 1, "T01_frame_square_b": 1, "T01_light_gate": 1}
GATE_ROWS_IN_LAYOUT = {"frame_8x10": 5, "frame_square_a": 2, "frame_square_b": 2, "light_gate": 1}
TOLERANCE = 1e-6


def load(variant):
    return json.loads((REPO / "assets-src" / "track" / f"{variant}.json").read_text(encoding="utf-8"))


@pytest.fixture(scope="module")
def built():
    return {v: track_kit.build(load(v)) for v in VARIANTS}


def expected_box(kind, r):
    """(min, max) of the kind's drawn body from its rows alone, where the origin sits as the object sheet says."""
    if kind == "concrete_block" or kind in ("board_large", "board_small", "notice_board"):
        return (-r["size_x_m"] / 2, 0.0, -r["size_z_m"] / 2), (r["size_x_m"] / 2, r["size_y_m"], r["size_z_m"] / 2)
    if "size_x_m" in r:
        return tuple(-r[f"size_{a}_m"] / 2 for a in "xyz"), tuple(r[f"size_{a}_m"] / 2 for a in "xyz")
    if kind == "pylon_3m":
        return (-r["base_w_m"] / 2, 0.0, -r["thick_m"] / 2), (r["base_w_m"] / 2, r["height_m"], r["thick_m"] / 2)
    if kind == "cone_light":
        return (-r["base_d_m"] / 2, 0.0, -r["base_d_m"] / 2), (r["base_d_m"] / 2, r["height_m"], r["base_d_m"] / 2)
    if kind.startswith("tyre_"):
        return (-r["outer_d_m"] / 2, 0.0, -r["outer_d_m"] / 2), (r["outer_d_m"] / 2, r["height_m"], r["outer_d_m"] / 2)
    if kind == "frame_8x10":
        return (-r["width_m"] / 2, 0.0, -r["depth_m"] / 2), (r["width_m"] / 2, r["height_m"], r["depth_m"] / 2)
    if kind in ("frame_square_a", "frame_square_b"):
        return (-r["head_w_m"] / 2, 0.0, -r["depth_m"] / 2), \
            (r["head_w_m"] / 2, r["leg_len_m"] + r["head_h_m"], r["depth_m"] / 2)
    if kind == "light_gate":
        return (-r["width_m"] / 2, 0.0, -r["depth_m"] / 2), (r["width_m"] / 2, r["height_m"], r["depth_m"] / 2)
    if kind == "led_ball":
        return (-r["diameter_m"] / 2,) * 3, (r["diameter_m"] / 2,) * 3
    if kind == "tent":
        return (-r["footprint_m"] / 2, 0.0, -r["footprint_m"] / 2), \
            (r["footprint_m"] / 2, r["height_m"], r["footprint_m"] / 2)
    return None  # the ribbons and the arc are measured along their lines


def test_every_drawn_kind_of_the_layout_has_its_variant():
    kinds = {o["kind"] for o in LAYOUT if o["drawn"]}
    assert len(kinds) == 24 and kinds == set(track_kit.KINDS)
    ribbons = {o["instance_id"] for o in LAYOUT if o["kind"] == "ribbon_tape"}
    wanted = {f"T01_{k}" for k in kinds if k != "ribbon_tape"} | {f"T01_ribbon_tape_{i}" for i in ribbons}
    assert set(VARIANTS) == wanted and len(VARIANTS) == 28


@pytest.mark.parametrize("variant", VARIANTS)
def test_params_conform_and_every_row_is_labelled(variant):
    params = load(variant)
    assert params_lib.validate(track_kit, params) == []
    assert all(row["label"] in track_kit.LABELS for row in params["rows"])


def test_a_gate_whose_members_disagree_is_refused():
    params = load("T01_frame_8x10")
    next(row for row in params["rows"] if row["name"] == "opening_w_m")["value"] = 2.20
    assert any("add up" in problem for problem in params_lib.validate(track_kit, params))


@pytest.mark.parametrize("variant", VARIANTS)
def test_the_drawn_size_is_the_rows(variant, built):
    params = load(variant)
    want = expected_box(params["kind"], track_kit.rows_of(params))
    if want is None:
        return
    for mesh in built[variant]["lods"]:
        lo, hi = mesh.bounds()
        assert lo == pytest.approx(want[0], abs=TOLERANCE) and hi == pytest.approx(want[1], abs=TOLERANCE)


def test_the_arc_is_its_length():
    r = track_kit.rows_of(load("T01_light_curve"))
    points = track_kit.arc_points(r)
    assert points[track_kit.ARC_SEGMENTS // 2] == pytest.approx((0.0, 0.0, 0.0), abs=TOLERANCE)
    theta = math.radians(r["arc_deg"])
    chord = 2 * (r["length_m"] / theta) * math.sin(theta / 2 / track_kit.ARC_SEGMENTS)
    assert sum(math.dist(a, b) for a, b in zip(points, points[1:])) == pytest.approx(chord * track_kit.ARC_SEGMENTS)
    assert chord * track_kit.ARC_SEGMENTS == pytest.approx(r["length_m"], rel=0.002)


def _inside(p, shape):
    """Whether p lies inside a collision shape."""
    d = [p[a] - shape["center"][a] for a in range(3)]
    if shape["shape"] == "sphere":
        return math.dist(p, shape["center"]) < shape["radius"]
    x, y, z, w = shape["rotation"]
    q = (-x, -y, -z, w)
    t = [2 * (q[1] * d[2] - q[2] * d[1]), 2 * (q[2] * d[0] - q[0] * d[2]), 2 * (q[0] * d[1] - q[1] * d[0])]
    local = [d[0] + w * t[0] + q[1] * t[2] - q[2] * t[1], d[1] + w * t[1] + q[2] * t[0] - q[0] * t[2],
             d[2] + w * t[2] + q[0] * t[1] - q[1] * t[0]]
    if shape["shape"] == "box":
        return all(abs(local[a]) < shape["size"][a] / 2 for a in range(3))
    if shape["shape"] == "cylinder":
        return abs(local[1]) < shape["height"] / 2 and math.hypot(local[0], local[2]) < shape["radius"]
    half = max(shape["height"] / 2 - shape["radius"], 0.0)
    return math.dist(local, (0.0, max(-half, min(half, local[1])), 0.0)) < shape["radius"]


def _gate_numbers(variant):
    r = track_kit.rows_of(load(variant))
    sill = {"T01_frame_8x10": 0.0, "T01_light_gate": r.get("rim_m", 0.0)}.get(
        variant, r.get("leg_len_m", 0.0) + r.get("banner_h_m", 0.0))
    return r["opening_w_m"], r["opening_h_m"], sill


@pytest.mark.parametrize("variant", sorted(GATES))
def test_each_gate_opening_is_its_rows(variant, built):
    width, height, sill = _gate_numbers(variant)
    gaps = built[variant]["gaps"]
    assert len(gaps) == 1 and gaps[0]["id"] == "opening" and gaps[0]["normal"] == [0.0, 0.0, 1.0]
    xs, ys = [p[0] for p in gaps[0]["polygon"]], [p[1] for p in gaps[0]["polygon"]]
    assert (min(xs), max(xs)) == pytest.approx((-width / 2, width / 2), abs=TOLERANCE)
    assert (min(ys), max(ys)) == pytest.approx((sill, sill + height), abs=TOLERANCE)
    assert gaps[0]["narrowest_m"] == pytest.approx(min(width, height), abs=TOLERANCE)


@pytest.mark.parametrize("variant", sorted(GATES))
def test_nothing_stands_in_a_gate_opening_and_its_members_close_it(variant, built):
    """A grid through the opening along its axis hits nothing; points just outside each side of it are in a member,
    except below a sill at the ground."""
    width, height, sill = _gate_numbers(variant)
    shapes = built[variant]["collision"]
    for i in range(11):
        for j in range(11):
            x = (i / 10 - 0.5) * (width - 0.002)
            y = sill + 0.001 + j / 10 * (height - 0.002)
            for z in (-0.3, -0.04, 0.0, 0.04, 0.3):
                assert not any(_inside((x, y, z), s) for s in shapes), (x, y, z)
    middle = sill + height / 2
    for p in ((-width / 2 - 0.01, middle, 0.0), (width / 2 + 0.01, middle, 0.0), (0.0, sill + height + 0.01, 0.0)):
        assert any(_inside(p, s) for s in shapes), p
    if sill > 0:
        assert any(_inside((0.0, sill - 0.01, 0.0), s) for s in shapes)


def test_the_layout_has_the_ten_gates_of_these_kinds():
    counts = {k: sum(1 for o in LAYOUT if o["kind"] == k) for k in GATE_ROWS_IN_LAYOUT}
    assert counts == GATE_ROWS_IN_LAYOUT and sum(counts.values()) == 10


@pytest.mark.parametrize("variant", [v for v in VARIANTS if "tyre" in v])
def test_a_tyre_hole_is_a_gap_of_its_diameter(variant, built):
    r = track_kit.rows_of(load(variant))
    gap = built[variant]["gaps"][0]
    assert gap["id"] == "hole" and gap["narrowest_m"] == pytest.approx(r["hole_d_m"])
    assert audit.inscribed_diameter(gap["polygon"], gap["normal"]) == pytest.approx(r["hole_d_m"], abs=0.005)
    for shape in built[variant]["collision"]:
        assert not _inside((0.0, r["height_m"] / 2, 0.0), shape)
        assert not _inside((r["hole_d_m"] / 2 - 0.001, r["height_m"] / 2, 0.0), shape)


@pytest.mark.parametrize("row", [o for o in LAYOUT if o["kind"] == "ribbon_tape"], ids=lambda o: str(o["instance_id"]))
def test_a_ribbon_follows_its_layout_row(row, built):
    variant = f"T01_ribbon_tape_{row['instance_id']}"
    params = load(variant)
    r = track_kit.rows_of(params)
    assert row["position_m"] == [0.0, 0.0, 0.0] and row["rotation_xyzw"] == [0.0, 0.0, 0.0, 1.0]
    points = track_kit.ribbon_points(row["instance_id"])
    source = [tuple(p["position_m"]) for p in row["points"]]
    assert all(p in source for p in points) and len(source) - len(points) <= 1
    length = sum(math.dist(a, b) for a, b in zip(points, points[1:]))
    assert 34.0 <= length <= 115.1
    data = built[variant]
    assert [s["shape"] for s in data["collision"]] == ["cylinder"] * len(points)  # the stakes; the band has none
    assert data["collision"][0]["radius"] == pytest.approx(r["stake_d_m"] / 2)
    assert data["collision"][0]["height"] == pytest.approx(r["stake_h_m"])
    assert [m["kind"] for m in data["micro"]] == ["band"] * len(data["lods"])
    band = data["micro"][0]["mesh"]
    for p in points:
        ys = [v[1] for v in band.vertices if math.hypot(v[0] - p[0], v[2] - p[2]) < r["band_w_m"]]
        assert min(ys) == pytest.approx(p[1] + r["band_lift_m"]) and \
            max(ys) == pytest.approx(p[1] + r["band_lift_m"] + r["band_t_m"])
    assert len(data["snag"]) == 1 and data["snag"][0]["material"] == "fabric"
    assert len(data["snag"][0]["points"]) == len(points)


@pytest.mark.parametrize("kind", ["led_flag_green", "led_flag", "light_curve"])
def test_flags_and_the_arc_carry_a_snag(kind, built):
    assert [s["kind"] for s in built[f"T01_{kind}"]["snag"]] == ["edge"]


def test_only_a_block_is_landable_and_it_has_a_solid_wind_box(built):
    data = built["T01_concrete_block"]
    assert [s["landable"] for s in data["collision"]] == [True]
    assert [w["porosity"] for w in data["wind_volumes"]] == [0.0]
    for variant, other in built.items():
        if variant != "T01_concrete_block":
            assert not any(s["landable"] for s in other["collision"]), variant


def test_palette_matches_its_recipe_and_the_catalog():
    recipe = json.loads((REPO / "assets-src" / "materials" / "track_kit.json").read_text(encoding="utf-8"))
    assert recipe["kind"] == "palette" and tuple(recipe["colours"]) == track_kit.COLOURS
    assert CATALOG["generator"] == "track_kit" and CATALOG["class"] == "track"
    assert set(CATALOG["materials"]) == set(track_kit.SLOTS)
    physics = json.loads((REPO / "assets-src" / "physics_materials.json").read_text(encoding="utf-8"))["materials"]
    assert set(track_kit.SLOTS) <= set(physics)


@pytest.mark.parametrize("variant", VARIANTS)
def test_metadata_validates_and_passes_the_build_audit(variant, built):
    params = load(variant)
    visuals = dict(CATALOG["materials"])
    data = meta.assemble("T01", variant, "track_kit", params, built[variant], visuals)
    assert meta.validate(data, meta.load_physics_materials(REPO)) == []
    problems, _ = audit.run(data, built[variant]["lods"][0], soft=audit.soft_materials(REPO))
    assert problems == []


def test_the_build_is_deterministic():
    for variant in ("T01_tyre_red", "T01_ribbon_tape_108", "T01_frame_square_b"):
        a, b = track_kit.build(load(variant)), track_kit.build(load(variant))
        assert [m.vertices for m in a["lods"]] == [m.vertices for m in b["lods"]]
        assert a["collision"] == b["collision"] and a["gaps"] == b["gaps"]
