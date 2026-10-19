"""generators.pole (p1-l01-l03-pole-wires, "L01 wooden pole"; design decisions 1-4, 9, 10): the committed params
conform and the defaults are L01_straight; the sizes are the sheet's; the brace gap is the real clear space, its
inscribed circle checked by hand and by the audit (and the sheet's 1.78 m traced to the axes' triangle at 4.9 m); the
four ports sit on the insulators' necks, outward; check refuses concrete_sv and everything else not built; the LODs
and the 1 km cull; the log's collision within 10 mm of the log both ways; every snag record names its material
(forward-compatible with p1-v01-locust); the leaning variant through the full audit. Margins are printed (TST-06)."""
import copy
import json
import math

import pytest

from generators import pole
from lib import audit, catalog, meta, params
from lib.geometry import Mesh

VARIANTS = ("L01_straight", "L01_leaning", "L01_stub")
VISUALS = {"wood": "pole_wood", "concrete": "pole_concrete", "steel": "pole_steel", "insulator": "insulator_porcelain",
           "ground_wire": "wire_steel"}
BUILT = {}


def load(repo_root, variant):
    return json.loads((repo_root / "assets-src" / "lines" / f"{variant}.json").read_text(encoding="utf-8"))


def build(repo_root, name):
    """(params, built, metadata) of a committed variant, built once per module."""
    if name not in BUILT:
        p = load(repo_root, name)
        built = pole.build(p)
        BUILT[name] = p, built, meta.assemble("L01", name, "pole", p, built, VISUALS)
    return BUILT[name]


def test_params_conform_and_the_defaults_are_the_straight_variant(repo_root):
    for name in VARIANTS:
        assert params.validate(pole, load(repo_root, name)) == [], name
    flat = params._flatten(load(repo_root, "L01_straight"))
    assert set(flat) == set(pole.PARAMS)
    for key, spec in pole.PARAMS.items():
        assert spec["default"] == flat[key], key
        if spec["type"] in ("int", "float"):
            assert spec["range"][0] <= spec["default"] <= spec["range"][1], key
    leaning, stub = load(repo_root, "L01_leaning"), load(repo_root, "L01_stub")
    assert (leaning["lean_deg"], leaning["brace"], leaning["head"]) == (9.0, "single", "crossarm_0p4")
    assert stub["pole_kind"] == "wood_on_stub" and stub["length_m"] == 7.5


def test_catalog_entry_and_the_wood_tile(repo_root):
    entry = catalog.load(repo_root)["assets"]["L01"]
    assert entry == {"class": "lines", "generator": "pole", "variants": list(VARIANTS), "materials": VISUALS}
    recipe = json.loads((repo_root / "assets-src" / "materials" / "pole_wood.json").read_text(encoding="utf-8"))
    assert tuple(recipe["tile_size_m"]) == pole.WOOD_TILE_M, "the wrap round the log closes on a whole tile"


def test_sizes_are_the_sheets(repo_root, material_ids):
    """§L01: 7.5 m above ground (9.5 m less 2.0 m, or 7.5 m on the stub), 0.16 m top, the stub 1.6 m showing, the
    crossarm 0.7 m (L 1: 0.6-0.8), the lean 9 degrees."""
    for name in VARIANTS:
        p, built, data = build(repo_root, name)
        assert meta.validate(data, material_ids) == [], name
        assert data["bounds"]["min"][1] == 0.0, name  # nothing drawn under the ground
    _, _, straight = build(repo_root, "L01_straight")
    assert straight["bounds"]["max"][1] == pytest.approx(7.5, abs=1e-6)
    tops = [v for v in build(repo_root, "L01_straight")[1]["lods"][0].vertices if v[1] == pytest.approx(7.5)]
    widest = max(math.hypot(v[0], v[2]) for v in tops if math.hypot(v[0], v[2]) < 0.1)
    assert widest == pytest.approx(0.08 * 2 / (1 + math.cos(math.pi / 12)), abs=1e-6)  # the top's 12-gon corners
    _, built, stub = build(repo_root, "L01_stub")
    concrete = [v for i, f in enumerate(built["lods"][0].faces) if f[3] == "concrete" for v in
                (built["lods"][0].vertices[j] for j in f[0])]
    assert max(v[1] for v in concrete) == pytest.approx(1.6) and min(v[1] for v in concrete) == 0.0
    assert max(v[0] for v in concrete) - min(v[0] for v in concrete) == pytest.approx(0.17)
    _, built, leaning = build(repo_root, "L01_leaning")
    box = next(c for c in leaning["collision"] if c["shape"] == "box")
    assert box["size"] == pytest.approx([0.1, 0.1, 0.7])
    top = pole.Pole(load(repo_root, "L01_leaning")).place((0.0, 7.5, 0.0))
    assert math.degrees(math.atan2(top[2], top[1])) == pytest.approx(9.0) and top[0] == pytest.approx(0.0)


def test_the_leaning_poles_crossarm_hangs_15_m_under_the_top_with_its_hooks_above(repo_root):
    """Task 1.3's look against L 1-3 (the crossarm 16-28 % of the visible pole under the top, 1.2-2 m): on
    L01_leaning `crossarm_below_m` puts the crossarm's top 1.5 m under the 7.5 m top, and its two hooks stand above it,
    0.3 and 0.8 m under the top; ports run from the top, the hooks' first, then the crossarm's two pins."""
    p, _, data = build(repo_root, "L01_leaning")
    assert p["crossarm_below_m"] == 1.5 and p["hooks"] == 2
    shape = pole.Pole(p)
    box = next(c for c in data["collision"] if c["shape"] == "box")
    centre = shape.place((-(shape.radius(5.95) + 0.05), 7.5 - 1.5 - 0.05, 0.0))
    assert box["center"] == pytest.approx(list(centre), abs=1e-6)
    unlean = pole.Pole(dict(p, lean_azimuth_deg=270.0))  # the same lean about the opposite axis: the inverse
    heights = [unlean.place(q["position"])[1] for q in data["ports"]]
    assert heights == pytest.approx([7.355, 6.855, 6.125, 6.125], abs=1e-6)
    assert min(heights[:2]) - 0.075 - 0.08 - pole.HOOK_D_M / 2 > 6.0 + 0.05 + 0.108  # hooks over the insulators
    print(f"L01_leaning: crossarm top at 6.000 m (upright), its pins' ports at 6.125 m, the hooks' at "
          f"{heights[0]:.3f} and {heights[1]:.3f} m")


def clear_triangle_by_hand(p):
    """The brace triangle's clear space from the sheet's construction, independently of the generator: in the
    brace's plane, the pole's inner surface (radius from the butt's 0.13 m to the top's 0.08 m over the 9.5 m log),
    the brace's inner surface (its 0.16 m log at 25 degrees, the axis through the pole's axis at 0.65 of 7.5 m) and
    the ground; returns its three corners and its inscribed circle's diameter."""
    h = p["length_m"] - p["burial_m"]
    taper = (p["top_section_m"] - p["butt_section_m"]) / 2 / p["length_m"]
    r_ground = p["butt_section_m"] / 2 + taper * p["burial_m"]
    beta = math.radians(p["brace_angle_deg"])
    attach = p["brace_attach"] * h
    # The brace axis meets the ground at attach * tan(beta); its inner surface is 0.08 m nearer the pole.
    foot = attach * math.tan(beta) - 0.08 / math.cos(beta)
    # The inner surfaces meet where x_pole(y) = r_ground + taper * y equals x_brace(y) = foot - y * tan(beta).
    apex_y = (foot - r_ground) / (taper + math.tan(beta))
    a, b, c = (r_ground, 0.0), (foot, 0.0), (r_ground + taper * apex_y, apex_y)
    sides = [math.dist(a, b), math.dist(b, c), math.dist(c, a)]
    area = abs((b[0] - a[0]) * (c[1] - a[1]) - (c[0] - a[0]) * (b[1] - a[1])) / 2
    return (a, b, c), 4 * area / sum(sides)


def inscribed_circle(a, b, c):
    """A right triangle's inscribed circle diameter, legs a and b and hypotenuse c: a + b - c."""
    return a + b - c


def test_brace_gap_is_the_real_clear_space_and_its_circle_checks(repo_root):
    """The scenario "Brace gap against the drone": one gap polygon, the triangle between pole, brace and ground, with
    narrowest_m the inscribed circle, and the audit's check agreeing within 5 mm. The polygon is the real clear space
    between the members' surfaces (the pipeline's gap convention), so its circle is 1.611 m, not the sheet's 1.78 m:
    that number is the triangle of the members' axes with the attachment rounded to 4.9 m (next test)."""
    p, built, data = build(repo_root, "L01_leaning")
    assert len(data["gaps"]) == 1
    gap = data["gaps"][0]
    corners, by_hand = clear_triangle_by_hand(p)
    assert gap["narrowest_m"] == pytest.approx(by_hand, abs=1e-5)
    assert gap["narrowest_m"] == pytest.approx(1.611, abs=0.0005)
    for got, want in zip(gap["polygon"], corners):  # the upright corners, leaned 9 degrees about X
        assert got == pytest.approx(list(pole.Pole(p).place((want[0], want[1], 0.0))), abs=1e-6)
    circle = 2 * audit._inscribed_radius(audit._flatten_polygon(gap["polygon"], gap["normal"]))
    assert abs(circle - gap["narrowest_m"]) <= audit.NARROWEST_TOLERANCE_M
    assert gap["id"] == "brace_triangle_any_yaw_below_2.95m_nose_first_below_3.24m"
    print(f"brace gap: narrowest_m {gap['narrowest_m']:.4f} m, by hand {by_hand:.4f} m, audit's circle "
          f"{circle:.4f} m (margin {(audit.NARROWEST_TOLERANCE_M - abs(circle - gap['narrowest_m'])) * 1000:.1f} mm "
          f"of 5); the spec's 1.78 m is {abs(1.78 - gap['narrowest_m']) * 1000:.0f} mm away")
    # The check fails a narrowest_m the polygon does not hold: the spec's 1.78 m on this clear space.
    wrong = copy.deepcopy(data)
    wrong["gaps"][0]["narrowest_m"] = 1.78
    problems, _ = audit.run(dict(wrong, collision=[]), Mesh())
    assert problems == [f"gaps[0] '{gap['id']}': narrowest_m 1.7800, largest inscribed circle {circle:.4f}: "
                        "they differ by more than 5 mm"]


def test_the_sheets_178_is_the_axes_triangle_at_49_m():
    """§L01's "attachment 4.9 m up, 25 degrees ... the largest inscribed circle 1.78 m": the triangle of the pole's and
    the brace's axes with the ground. At 0.65 of 7.5 m (4.875 m) it is 1.769 m; at the rounded 4.9 m, 1.778 m."""
    for height, want in ((4.9, 1.778), (4.875, 1.769)):
        base = height * math.tan(math.radians(25.0))
        circle = inscribed_circle(height, base, math.hypot(height, base))
        assert circle == pytest.approx(want, abs=0.0005)
        print(f"axes' triangle at {height} m: {circle:.4f} m")


def test_four_ports_sit_on_the_insulator_necks_outward(repo_root, material_ids):
    """The scenario "Ports written and read": a pole with four hooks lists four ports at the insulators, outward:
    hook_0 the top one on +Z, each next 0.5 m lower on the other side, at the neck groove 0.019 m off the insulator's
    axis (the top hook 0.3 m under the 7.5 m top, the insulator 0.08 m up the hook, its neck 0.075 m up it), each
    seating a conductor up to the groove's 14 mm (task 1.5's reading §1); bare pins seat none and have no port."""
    _, built, data = build(repo_root, "L01_straight")
    ports = data["ports"]
    assert [q["id"] for q in ports] == ["hook_0", "hook_1", "hook_2", "hook_3"]
    assert [q["position"][1] for q in ports] == pytest.approx([7.355, 6.855, 6.355, 5.855], abs=1e-6)
    assert [q["direction"] for q in ports] == [[0.0, 0.0, 1.0], [0.0, 0.0, -1.0], [0.0, 0.0, 1.0], [0.0, 0.0, -1.0]]
    assert [q["max_diameter_m"] for q in ports] == [0.014] * 4  # the TF-20 groove, 0.068-0.082 m up the insulator
    cylinders = [c for c in data["collision"] if c["shape"] == "cylinder" and c["material"] == "glass"]
    assert len(cylinders) == 4
    for port, cyl in zip(ports, cylinders):
        axis_off = math.hypot(port["position"][0] - cyl["center"][0], port["position"][2] - cyl["center"][2])
        assert axis_off == pytest.approx(pole.NECK_R_M, abs=1e-6)
        assert port["position"][2] * port["direction"][2] > abs(cyl["center"][2]) * 0.99  # outward of its insulator
        assert cyl["radius"] == pytest.approx(0.0375) and cyl["height"] == pytest.approx(0.108)
    assert pole.build(dict(load(repo_root, "L01_straight"), insulator="none"))["ports"] == []
    _, _, leaning = build(repo_root, "L01_leaning")
    assert len(leaning["ports"]) == 4 and meta.validate(leaning, material_ids) == []
    for q in leaning["ports"]:  # leaned with the pole: still unit, tipped 9 degrees
        assert math.hypot(*q["direction"]) == pytest.approx(1.0, abs=1e-6)
        assert abs(q["direction"][1]) == pytest.approx(math.sin(math.radians(9.0)), abs=1e-6)


@pytest.mark.parametrize("edit, expected", [
    ({"pole_kind": "concrete_sv"},
     "'pole_kind' = 'concrete_sv' is not built yet (concrete_sv is L02's task on this generator); use one of "
     "wood_log, wood_on_stub"),
    ({"brace": "a_frame"}, "'brace' = 'a_frame' is not built yet"),
    ({"head": "crossarm_10kv"}, "'head' = 'crossarm_10kv' is not built yet"),
    ({"insulator": "glass"}, "'insulator' = 'glass' is not built yet (glass has no recipe yet)"),
    ({"length_m": 7.5}, "'length_m' = 7.5 is outside [8.5, 11.0] for wood_log"),
    ({"stub_above_m": 1.8}, "'stub_above_m' = 1.8 is not read here (wood_log, brace none, head hooks_0p4); keep 1.6"),
    ({"brace": "single", "lean_deg": 9.0, "lean_azimuth_deg": 45.0},
     "'lean_azimuth_deg' = 45.0: a braced pole leans only across its brace (90 or 270)"),
    ({"damage": 0.5}, "'damage' = 0.5 asks for the D or X state, not built yet; keep it under 0.25 (I)"),
    ({"weathering": 0.8}, "'weathering' = 0.8 is not built yet"),
    ({"head": "none"}, "'hooks' = 4 needs a head; head none is a bare pole"),
    ({"brace": "single", "top_section_m": 0.14, "brace_attach": 0.8},
     "the brace's end (0.16 m) would show beside the pole"),
    ({"crossarm_below_m": 1.0}, "'crossarm_below_m' = 1.0 is not read here (wood_log, brace none, head hooks_0p4); "
                                "keep 1.5"),
    ({"head": "crossarm_0p4", "hooks": 2, "crossarm_below_m": 0.25},
     "'hooks' = 2: the lowest hook (6.692 m) would not clear the crossarm's insulators (7.408 m)"),
])
def test_check_refuses_what_is_not_built_or_does_not_hold(repo_root, edit, expected):
    problems = params.validate(pole, dict(load(repo_root, "L01_straight"), **edit))
    assert any(q.startswith(expected) for q in problems), problems


def test_lods_and_the_night_silhouette(repo_root):
    """The scenario "Night silhouette kept to 1 km": LOD1 at 35 m (a 0.075 m insulator under 2 px at 935 px/rad),
    LOD2 a 6-sided prism of the log alone, culled beyond 1 000 m."""
    assert 0.075 * 935 / 35.0 == pytest.approx(2.0, abs=0.01)
    for name in VARIANTS:
        _, built, data = build(repo_root, name)
        assert [lod["switch_distance_m"] for lod in data["lods"]] == [0.0, 35.0, 120.0]
        assert data["cull_distance_m"] == 1200.0 >= 1000.0
        lod1, lod2 = built["lods"][1], built["lods"][2]
        assert set(lod1.slots()) <= {"wood", "concrete"} and lod2.slots() == ["wood"]
        assert data["lods"][2]["tris"] == 16  # 6 quads and the top's 6-gon
        tris = [lod["tris"] for lod in data["lods"]]
        assert tris[0] > tris[1] > tris[2], tris
        print(f"{name}: triangles per LOD {tris}")


def test_the_log_collision_is_within_10mm_of_the_log_both_ways(repo_root):
    """§L01: the log as stacked segments with their real radius, within 10 mm of the visual: the audit run on the
    log's own faces and segments (the brace, head and wire left out) measures both directions."""
    for name in ("L01_straight", "L01_stub"):
        p, built, data = build(repo_root, name)
        n = pole.Pole(p).segments()
        assert n == {"L01_straight": 3, "L01_stub": 4}[name]
        log = Mesh()
        log.vertices = built["lods"][0].vertices
        log.faces = [f for f in built["lods"][0].faces if f[3] == "wood"]
        segments = data["collision"][:n]
        assert [c["shape"] for c in segments] == ["capsule"] * (n - 1) + ["cylinder"]
        assert all(c["material"] == "timber" for c in segments)
        problems, lines = audit.run(dict(data, collision=segments, gaps=[]), log, planted=True)
        assert problems == []
        worst = max(float(x) for line in lines[1:] for x in [line.split("->")[1].split(" mm")[0].split()[-1]])
        assert worst <= 10.0, lines
        print(f"{name}: the log's {n} segments within {worst:.3f} mm of its faces (margin {10 - worst:.3f} mm)")


def test_snag_records_carry_the_sheets_kinds_radii_and_materials(repo_root):
    """Hooks and pins `hook` 8.5 mm steel (the bar's half thickness, task 1.5's reading §6), the insulators' drip
    edges `edge` 5 mm glass, crossarm ends 2 mm timber, bands 3 mm steel, the grounding wire 3 mm cable; every record
    names its material (p1-v01-locust's rule, written now)."""
    want = {("hook", 0.0085, "steel"), ("edge", 0.005, "glass"), ("edge", 0.002, "timber"), ("edge", 0.003, "steel"),
            ("edge", 0.003, "cable")}
    counts = {}
    for name in VARIANTS:
        _, _, data = build(repo_root, name)
        for record in data["snag"]:
            key = (record["kind"], record["radius_m"], record["material"])
            assert key in want, (name, key)
            counts.setdefault(name, {}).setdefault(key, 0)
            counts[name][key] += 1
        assert all(not c["landable"] for c in data["collision"])
        assert [(w["shape"], w["porosity"]) for w in data["wind_volumes"]] == [("box", 0.0)]
        hooks = [c["radius"] for c in data["collision"] if c["material"] == "steel"]
        assert set(hooks) == {pole.HOOK_D_M / 2}, "a hook record's reach is its capsule's"
    assert counts["L01_straight"] == {("hook", 0.0085, "steel"): 4, ("edge", 0.005, "glass"): 4,
                                      ("edge", 0.003, "cable"): 1}
    assert counts["L01_leaning"] == {("hook", 0.0085, "steel"): 4, ("edge", 0.005, "glass"): 4,
                                     ("edge", 0.002, "timber"): 2, ("edge", 0.003, "cable"): 1}
    assert counts["L01_stub"][("edge", 0.003, "steel")] == 2


def test_drip_edges_and_bands_lie_where_the_fiber_meets_them(repo_root):
    """Task 1.5's reading §6, items 6 and 7: each insulator's ring runs round its skirt's drip edge, 32.5 mm from its
    axis and 5 mm up its base, so its 5 mm reach is flush with the 37.5 mm cylinder's side and bottom (the old neck
    ring lay 13.5 mm inside it); each band's record runs on the strap's centre line, half its 6 mm out of the outline
    it is wrapped on, so its 3 mm reach is flush with both faces."""
    _, _, data = build(repo_root, "L01_straight")
    cylinders = [c for c in data["collision"] if c["shape"] == "cylinder" and c["material"] == "glass"]
    rings = [s for s in data["snag"] if s["material"] == "glass"]
    assert len(rings) == len(cylinders) == 4
    for ring, cyl in zip(rings, cylinders):
        base = cyl["center"][1] - cyl["height"] / 2
        for q in ring["points"]:
            reach = math.hypot(q[0] - cyl["center"][0], q[2] - cyl["center"][2]) + ring["radius_m"]
            assert reach == pytest.approx(cyl["radius"], abs=1e-6)  # flush with the side at the ring's corners
            assert q[1] - ring["radius_m"] == pytest.approx(base, abs=1e-6)  # and with the bottom
    p, built, stub = build(repo_root, "L01_stub")
    shape = pole.Pole(p)
    bands = [s for s in stub["snag"] if s["material"] == "steel" and s["kind"] == "edge"]
    assert len(bands) == 2
    for band in bands:
        height = band["points"][0][1]
        on_log = [q for q in band["points"] if q[0] < -0.01]  # the -X half wraps the log alone
        assert on_log
        for q in on_log:  # the outline's 32-gon corners plus half the strap, its thickness square to the edges
            out = math.hypot(q[0], q[2]) - shape.radius(height)
            assert out == pytest.approx(pole.BAND_T_M / 2 / math.cos(math.pi / 32), abs=1e-6)
    print(f"drip rings at r {pole.INSULATOR_R_M - 0.005:.4f} m, 0.005 m up; band centre lines "
          f"{pole.BAND_T_M / 2 / math.cos(math.pi / 32) * 1000:.3f} mm out")


def test_build_is_deterministic_and_the_seed_moves_only_the_wood_uvs(repo_root):
    p = load(repo_root, "L01_straight")
    a, b, c = pole.build(p), pole.build(copy.deepcopy(p)), pole.build(dict(p, seed=7))
    assert a["lods"][0].vertices == b["lods"][0].vertices and a["lods"][0].faces == b["lods"][0].faces
    assert a["lods"][0].vertices == c["lods"][0].vertices
    wood = [f[2] for f in a["lods"][0].faces if f[3] == "wood"]
    assert wood != [f[2] for f in c["lods"][0].faces if f[3] == "wood"]
    assert [f[2] for f in a["lods"][0].faces if f[3] == "steel"] == [f[2] for f in c["lods"][0].faces if f[3] == "steel"]


def test_the_leaning_variant_passes_the_full_audit_planted(repo_root):
    """L01_leaning through lib.audit as build.py runs it: every primitive within 50 mm, within 20 mm near the brace
    gap (the log, the brace, its buried foot), the gap's circle agreeing; the lines are printed."""
    p, built, data = build(repo_root, "L01_leaning")
    problems, lines = audit.run(data, built["lods"][0], soft=audit.soft_materials(repo_root), planted=True)
    for line in lines:
        print(line)
    assert problems == []
    near = [line for line in lines if "near gaps/landable" in line]
    assert len(near) >= 4  # the two lower log capsules, the brace and the mesh
