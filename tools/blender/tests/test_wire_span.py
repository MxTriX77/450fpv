"""generators.wire_span (p1-l01-l03-pole-wires, "L03 wire span", "Wires are audited against their polyline"; design
decisions 5-7, 10): the committed params conform and the defaults are L03_medium; the medium span hangs from
L01_straight's four ports and sags 0.8 m, its lowest points at the ports less 0.8 m within 10 mm; the record's points
are the tube's centre line; the catenary holds its sag between unequal ends; the polyline audit passes the built
tubes under 5 mm and fails a vertex moved 20 mm out, naming the wire, and keeps the tubes out of the primitive audit;
a SIP drop is two twisted cores; check refuses what is not built or does not hold; the LODs, records and snags. Margins
are printed (TST-06)."""
import copy
import json
import math

import pytest

from generators import debris_brick, pole, wire_span
from lib import audit, catalog, meta, params
from lib.geometry import Mesh

VARIANTS = ("L03_thin", "L03_medium", "L03_thick")
VISUALS = {"steel": "wire_steel", "aluminium": "wire_aluminium", "insulated": "wire_sip"}
BUILT = {}


def load(repo_root, variant):
    return json.loads((repo_root / "assets-src" / "lines" / f"{variant}.json").read_text(encoding="utf-8"))


def build(repo_root, name):
    """(params, built, metadata) of a committed variant, built once per module."""
    if name not in BUILT:
        p = load(repo_root, name)
        built = wire_span.build(p)
        BUILT[name] = p, built, meta.assemble("L03", name, "wire_span", p, built, VISUALS)
    return BUILT[name]


def test_params_conform_and_the_defaults_are_the_medium_variant(repo_root):
    for name in VARIANTS:
        assert params.validate(wire_span, load(repo_root, name)) == [], name
    flat = params._flatten(load(repo_root, "L03_medium"))
    assert set(flat) == set(wire_span.PARAMS)
    for key, spec in wire_span.PARAMS.items():
        assert spec["default"] == flat[key], key
        if spec["type"] in ("int", "float"):
            assert spec["range"][0] <= spec["default"] <= spec["range"][1], key
    thin, thick = load(repo_root, "L03_thin"), load(repo_root, "L03_thick")
    assert (thin["diameter_mm"], thin["conductor_type"], thin["conductors"]) == (5.0, "steel_wire", 1)
    assert (thick["diameter_mm"], thick["conductor_type"], thick["arrangement"], thick["span_m"]) == \
        (20.0, "sip_drop", "bundle", 20.0)


def test_catalog_entry(repo_root):
    entry = catalog.load(repo_root)["assets"]["L03"]
    assert entry == {"class": "lines", "generator": "wire_span", "variants": list(VARIANTS), "materials": VISUALS}


def test_medium_span_hangs_from_l01_ports_and_sags_08_m(repo_root, material_ids):
    """The scenario "Catenary and clearance": 40 m, four bare 9 mm conductors hung from L01_straight's four ports
    (7.355 m stepping down 0.5 m), sag 2 %: each end is its port plus one radius outward, each lowest point lies
    within 10 mm of the port's height less 0.8 m (6.555, 6.055, 5.555, 5.055 m), and the lowest conductor clears
    the 3.5 m check holds for bare wires."""
    p, built, data = build(repo_root, "L03_medium")
    assert meta.validate(data, material_ids) == []
    ports = pole.build(json.loads((repo_root / "assets-src" / "lines" / "L01_straight.json").read_text("utf-8")))["ports"]
    assert [q["position"][1] for q in ports] == pytest.approx([7.355, 6.855, 6.355, 5.855], abs=1e-9)
    wires = data["wires"]
    assert len(wires) == 4
    radius = 0.0045
    for wire, port, want in zip(wires, ports, (6.555, 6.055, 5.555, 5.055)):
        centre = [port["position"][a] + radius * port["direction"][a] for a in range(3)]
        assert wire["points"][0] == pytest.approx(centre, abs=1e-5)  # the first pole
        assert wire["points"][-1] == pytest.approx([40.0, centre[1], centre[2]], abs=1e-5)  # the second, 40 m on
        lowest = min(q[1] for q in wire["points"])
        assert abs(lowest - want) <= 0.010
        print(f"{port['id']}: port {port['position'][1]:.3f} m, lowest {lowest:.6f} m, want {want} m "
              f"(margin {(0.010 - abs(lowest - want)) * 1000:.3f} mm of 10)")
    assert min(q[1] for q in wires[3]["points"]) >= wire_span.CLEARANCE_M["bare_al"] == 3.5
    assert [len(w["points"]) for w in wires] == [81] * 4  # points_per_m 2 over 40 m
    assert all(w["sag_m"] == 0.8 and w["diameter_m"] == 0.009 and w["mass_kg_per_m"] == 0.135 for w in wires)


def test_records_reproduce_the_tubes_centre_line(repo_root):
    """The same scenario: the `wires` record's points are the tube's centre line within 5 mm (each LOD0 ring's
    centroid, the rings laid down conductor by conductor, six vertices each)."""
    _, built, data = build(repo_root, "L03_medium")
    vertices = built["lods"][0].vertices
    worst = 0.0
    for n, wire in enumerate(data["wires"]):
        for i, q in enumerate(wire["points"]):
            ring = vertices[(n * 81 + i) * 6:(n * 81 + i + 1) * 6]
            centroid = [sum(v[a] for v in ring) / 6 for a in range(3)]
            worst = max(worst, math.dist(centroid, q))
    assert worst <= 0.005
    print(f"record vs ring centroids: {worst * 1000:.4f} mm (margin {5 - worst * 1000:.4f} mm of 5)")


def test_catenary_holds_its_sag_between_unequal_ends():
    """A drop from 6.355 m to 3.5 m over 20 m at 2 %: it passes through both ends, hangs 0.4 m below the chord at
    mid-span, and its lowest point is the house end (still falling there)."""
    curve = wire_span.Catenary((0.0, 6.355, 0.3), (20.0, 3.5, 0.3), 0.4)
    assert curve.y(0.0) == pytest.approx(6.355, abs=1e-12) and curve.y(20.0) == pytest.approx(3.5, abs=1e-9)
    assert (6.355 + 3.5) / 2 - curve.y(10.0) == pytest.approx(0.4, abs=1e-9)
    assert curve.m > 20.0 and curve.lowest() == 3.5
    level = wire_span.Catenary((0.0, 7.0, 0.0), (40.0, 7.0, 0.0), 0.8)
    assert level.m == pytest.approx(20.0, abs=1e-9) and level.lowest() == pytest.approx(6.2, abs=1e-9)
    # the parabola's c = L^2 / 8f = 250 m; the catenary's is a little more
    assert level.c == pytest.approx(250.13, abs=0.01)


def test_polyline_audit_passes_the_built_tubes_under_5_mm(repo_root):
    """The scenario "Tube follows the polyline": the 40 m, 9 mm conductors pass with a largest deviation under 5 mm,
    printed per wire; the tubes leave nothing to the primitive audit."""
    _, built, data = build(repo_root, "L03_medium")
    problems, lines = audit.run(data, built["lods"][0], soft=audit.soft_materials(repo_root))
    for line in lines:
        print(line)
    assert problems == []
    assert len(lines) == 4 and all(line.startswith(f"audit wires[{i}] 9 mm (slot aluminium): largest deviation ")
                                   for i, line in enumerate(lines))
    for line in lines:
        deviation = float(line.split("largest deviation ")[1].split(" mm")[0])
        assert deviation < 5.0
        assert line.endswith(" over 486 vertices, tolerance 5 mm")  # 81 rings of 6
    # the ring's corners stand (pi / 6) / sin(pi / 6) of the radius out: 0.212 mm on 4.5 mm
    assert float(lines[0].split("largest deviation ")[1].split(" mm")[0]) == \
        pytest.approx(4.5 * (math.pi / 6 / math.sin(math.pi / 6) - 1), abs=0.005)


def test_polyline_audit_fails_a_vertex_20_mm_off_naming_the_wire(repo_root):
    """The scenario "Tube off its polyline": one vertex of the third conductor moved 20 mm out from its centre line
    fails the audit naming the wire, its deviation (20 mm plus the corner's 0.212 mm) and the tolerance."""
    _, built, data = build(repo_root, "L03_medium")
    lod0 = copy.deepcopy(built["lods"][0])
    k = (2 * 81 + 40) * 6  # the third conductor's mid-span ring, its first corner
    centre = data["wires"][2]["points"][40]
    v = lod0.vertices[k]
    out = [v[a] - centre[a] for a in range(3)]
    reach = math.hypot(*out)
    lod0.vertices[k] = tuple(v[a] + 0.020 * out[a] / reach for a in range(3))
    problems, lines = audit.run(data, lod0)
    assert len(problems) == 1
    assert problems[0].startswith("wires[2] 9 mm (slot aluminium): largest deviation 20.212 mm at (20, ")
    assert problems[0].endswith(" over 486 vertices exceeds the tolerance 5 mm")
    print(problems[0])


def test_wire_tubes_stay_out_of_the_primitive_audit(repo_root, k01_params):
    """The tube's vertices are excluded from the primitive audit: a brick with its box beside the span measures as
    K01 alone does (3.288 mm and about 1.9 mm), not the metres to the wires."""
    _, built, data = build(repo_root, "L03_medium")
    brick = debris_brick.build(k01_params)
    mesh = copy.deepcopy(built["lods"][0])
    base = len(mesh.vertices)
    mesh.vertices += brick["lods"][0].vertices
    mesh.faces += [([i + base for i in indices], n, uvs, slot) for indices, n, uvs, slot in brick["lods"][0].faces]
    both = dict(data, collision=brick["collision"],
                materials=data["materials"] + [{"slot": "brick", "visual": "brick_red", "physics": "masonry"}])
    problems, lines = audit.run(both, mesh)
    assert problems == []
    assert lines[0].startswith("audit collision[0] box: box->mesh 3.288 mm")
    assert lines[1].startswith("audit mesh: mesh->collision 1.8") and "nearest collision[0]" in lines[1]
    assert sum(line.startswith("audit wires[") for line in lines) == 4


def test_a_wire_without_a_tube_fails(repo_root):
    _, built, data = build(repo_root, "L03_thin")
    problems, _ = audit.run(data, Mesh())
    assert problems == ["wires[0] 5 mm (slot steel): no LOD0 vertex of its slot lies nearest to it, so it has no tube"]


def test_sip_drop_is_two_twisted_cores_within_its_bundle(repo_root, material_ids):
    """L03_thick: a 20 mm SIP drop (two 10 mm cores 5 mm off the axis, twisted at 0.45 m) from L01_straight's hook_2
    at 6.355 m to a house at 3.5 m over 20 m; the audit holds both cores to the bundle's 10 mm envelope; its lowest
    point is the house end, over the 2.75 m a drop keeps at the house entry."""
    p, built, data = build(repo_root, "L03_thick")
    assert meta.validate(data, material_ids) == []
    lines, core_r = wire_span.twisted_cores([(0.0, 5.0, 0.0), (1.0, 5.0, 0.0)], 0.020, 2, 0.45, 0.0)
    assert core_r == pytest.approx(0.005) and math.dist(lines[0][0], (0.0, 5.0, 0.0)) == pytest.approx(0.005)
    assert math.dist(lines[0][0], lines[1][0]) == pytest.approx(0.010)
    wire = data["wires"][0]
    assert wire["slot"] == "insulated" and wire["diameter_m"] == 0.02 and len(wire["points"]) == 41
    assert wire["points"][0] == pytest.approx([0.0, 6.355, 0.255842 + 0.010], abs=1e-6)
    assert min(q[1] for q in wire["points"]) == 3.5 >= wire_span.CLEARANCE_M["sip_drop"]
    problems, report = audit.run(data, built["lods"][0])
    print(report[0])
    assert problems == [] and report[0].startswith("audit wires[0] 20 mm (slot insulated): largest deviation 0.")
    arc = sum(math.dist(q, r) for q, r in zip(wire["points"][1:], wire["points"]))
    assert math.ceil(arc / (0.45 / 8)) == 360  # 8 rings a turn of the 20.2 m bundle
    assert data["lods"][0]["tris"] == 2 * (360 * 6 * 2 + 2 * 4)  # two 6-sided cores, capped


@pytest.mark.parametrize("edit, expected", [
    ({"damage": "cut"}, "'damage' = 'cut' is not built (cut and down are L04's and L05's states, not built yet)"),
    ({"arrangement": "horizontal"}, "'arrangement' = 'horizontal' is not built"),
    ({"class": "thin"}, "'diameter_mm' = 9.0 is outside [3.0, 6.5] for class thin"),
    ({"conductor_type": "sip_main"}, "'arrangement' = 'vertical_zigzag' with sip_main: a SIP hangs as a bundle"),
    ({"twist_pitch_m": 0.3}, "'twist_pitch_m' = 0.3 is not read here (4 conductor(s) of bare_al); keep 0.45"),
    ({"sag_ratio": 0.06}, "'sag_ratio' = 0.06 is over the intact 0.05"),
    ({"attach_heights_m": {"start": 4.0, "end": 4.0}},
     "conductor 1's lowest point 2.700 m is under the 3.5 m clearance of bare_al"),
    ({"lod": {"lod1_switch_m": 80.0, "cull_m": 60.0}}, "'lod.*' must increase: 80.0, 60.0"),
])
def test_check_refuses_what_is_not_built_or_does_not_hold(repo_root, edit, expected):
    problems = params.validate(wire_span, dict(load(repo_root, "L03_medium"), **edit))
    assert any(q.startswith(expected) for q in problems), problems


def test_check_refuses_a_long_drop_and_idle_keys_on_a_single_conductor(repo_root):
    thick = load(repo_root, "L03_thick")
    assert params.validate(wire_span, dict(thick, span_m=30.0))[0].startswith(
        "'span_m' = 30.0: a service drop spans 25 m at most")
    thin = load(repo_root, "L03_thin")
    assert params.validate(wire_span, dict(thin, spacing_m=0.6)) == [
        "'spacing_m' = 0.6 is not read here (1 conductor(s) of steel_wire); keep 0.5"]
    assert params.validate(wire_span, dict(thin, conductors=2)) == []  # two broadcast wires on hook_0 and hook_1


def test_lods_records_and_snags(repo_root):
    """LOD0 6-sided tubes, LOD1 4-sided from 20 m, culled at 300 m; per conductor a cable `wires` record and an
    `edge` snag along the same points with the wire's radius; no collision, gap, wind volume or port."""
    for name in VARIANTS:
        _, built, data = build(repo_root, name)
        assert [lod["switch_distance_m"] for lod in data["lods"]] == [0.0, 20.0]
        assert data["cull_distance_m"] == 300.0
        assert data["collision"] == data["gaps"] == data["wind_volumes"] == data["ports"] == []
        assert [m["physics"] for m in data["materials"]] == ["cable"]
        assert len(data["snag"]) == len(data["wires"])
        for wire, snag in zip(data["wires"], data["snag"]):
            assert snag == {"kind": "edge", "points": wire["points"], "radius_m": wire["diameter_m"] / 2,
                            "material": "cable"}
            assert wire["material"] == "cable" and wire["damage"] == "intact"
        tris = [lod["tris"] for lod in data["lods"]]
        assert tris[0] > tris[1]
        print(f"{name}: triangles per LOD {tris}")
    assert [build(repo_root, n)[2]["lods"][0]["tris"] for n in VARIANTS] == [968, 3872, 8656]
    assert [build(repo_root, n)[2]["lods"][1]["tris"] for n in VARIANTS] == [644, 2576, 324]


def test_build_is_deterministic_and_the_seed_moves_only_uvs_and_the_twist(repo_root):
    p = load(repo_root, "L03_medium")
    a, b, c = wire_span.build(p), wire_span.build(copy.deepcopy(p)), wire_span.build(dict(p, seed=7))
    assert a["lods"][0].vertices == b["lods"][0].vertices and a["lods"][0].faces == b["lods"][0].faces
    assert a["lods"][0].vertices == c["lods"][0].vertices and a["wires"] == c["wires"]
    assert [f[2] for f in a["lods"][0].faces] != [f[2] for f in c["lods"][0].faces]
    thick = load(repo_root, "L03_thick")
    assert wire_span.build(thick)["lods"][0].vertices != wire_span.build(dict(thick, seed=7))["lods"][0].vertices
