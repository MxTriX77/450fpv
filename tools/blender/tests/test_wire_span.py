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
        (15.0, "sip_drop", "bundle", 20.0)  # the sheet's 2 x 16 mm² drop
    assert thick["mass_kg_per_m"] == 0.1285  # published 2 x 16: 12.85 kg a 100 m coil (task 1.5's reading)


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


def test_tube_vertices_carry_the_thin_wire_inputs(repo_root):
    """Task 1.4's contract: every tube vertex lies on its ring the conductor's radius from the centre line, with its
    radial unit normal (square to the wire, pointing from the centre to the vertex) and (r, 0) as its second UV, at
    both LODs; a SIP core's radius is the core's (3.75 mm in the 15 mm drop) at LOD0 and the bundle's at LOD1."""
    for name, radii in (("L03_medium", (0.0045, 0.0045)), ("L03_thick", (0.00375, 0.0075))):
        _, built, _ = build(repo_root, name)
        for lod, radius in zip(built["lods"], radii):
            assert len(lod.normals) == len(lod.uv2) == len(lod.vertices)
            assert set(lod.uv2.values()) == {(radius, 0.0)}
            for k, v in enumerate(lod.vertices):
                n = lod.normals[k]
                assert math.hypot(*n) == pytest.approx(1.0, abs=1e-12)
    _, built, _ = build(repo_root, "L03_medium")
    lod0 = built["lods"][0]
    worst = 0.0
    for n_wire, wire in enumerate(built["wires"]):  # the unrounded points
        for i, q in enumerate(wire["points"]):
            for j in range(6):
                k = (n_wire * 81 + i) * 6 + j
                offset = [lod0.vertices[k][a] - q[a] for a in range(3)]
                worst = max(worst, abs(math.hypot(*offset) - 0.0045))
                assert [offset[a] / 0.0045 for a in range(3)] == pytest.approx(list(lod0.normals[k]), abs=1e-6)
    assert worst < 1e-6
    print(f"L03_medium: tube corners within {worst * 1e6:.4f} um of the 4.5 mm radius, normals radial")


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
    """The scenario "Tube follows the polyline": the 40 m, 9 mm conductors pass with a largest deviation under their
    4.5 mm tolerance (min(5 mm, D/2)), printed per wire; the tubes leave nothing to the primitive audit."""
    _, built, data = build(repo_root, "L03_medium")
    problems, lines = audit.run(data, built["lods"][0], soft=audit.soft_materials(repo_root))
    for line in lines:
        print(line)
    assert problems == []
    assert len(lines) == 4 and all(line.startswith(f"audit wires[{i}] 9 mm (slot aluminium): largest deviation ")
                                   for i, line in enumerate(lines))
    for line in lines:
        deviation = float(line.split("largest deviation ")[1].split(" mm")[0])
        assert deviation == 0.0  # every corner on the conductor's circle (the thin-wire contract)
        assert line.endswith(" over 486 vertices, its tube reaches 4.500 mm of the record's 4.5 mm radius, "
                             "tolerance 4.5 mm")  # 81 rings of 6, every corner on the circle


def test_polyline_audit_fails_a_vertex_20_mm_off_naming_the_wire(repo_root):
    """The scenario "Tube off its polyline": one vertex of the third conductor moved 20 mm out from its centre line
    fails the audit naming the wire, its deviation and the tolerance."""
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
    assert problems[0].startswith("wires[2] 9 mm (slot aluminium): largest deviation 20.000 mm at (20, ")
    assert problems[0].endswith(" over 486 vertices exceeds the tolerance 4.5 mm")
    print(problems[0])


def moved_out(built, data, wire, ring, by):
    """LOD0 of a built span with the first corner of `wire`'s ring `ring` moved `by` metres out from its centre line
    (single-strand conductors laid down one after another, 6 corners a ring)."""
    lod0 = copy.deepcopy(built["lods"][0])
    rings = len(data["wires"][0]["points"])
    k = (wire * rings + ring) * 6
    centre = data["wires"][wire]["points"][ring]
    v = lod0.vertices[k]
    out = [v[a] - centre[a] for a in range(3)]
    reach = math.hypot(*out)
    lod0.vertices[k] = tuple(v[a] + by * out[a] / reach for a in range(3))
    return lod0


def test_the_tolerance_is_half_a_thin_wires_diameter(repo_root):
    """Task 1.5's reading §3, §8 item 8: the tolerance is min(5 mm, D/2), so a drawn thin wire always overlaps its
    physical one: a corner 3 mm out fails the 5 mm wire (2.5 mm) and passes the 9 mm one (4.5 mm); each wire's line
    prints its own tolerance."""
    for name, wire, tolerance, fails in (("L03_thin", 0, 2.5, True), ("L03_medium", 1, 4.5, False)):
        _, built, data = build(repo_root, name)
        problems, lines = audit.run(data, moved_out(built, data, wire, 40, 0.003))
        line = next(q for q in lines if q.startswith(f"audit wires[{wire}] "))
        assert line.endswith(f", tolerance {tolerance:g} mm")
        assert ("largest deviation 3.000 mm" in line) and (len(problems) == (1 if fails else 0)), (line, problems)
        print(line)
        if fails:
            assert problems[0].endswith(f"exceeds the tolerance {tolerance:g} mm")


def test_a_record_wider_than_its_tube_fails(repo_root):
    """Review S1, the reading's N-5: each wire's farthest tube vertex reaches the record's radius within the tolerance
    min(5 mm, D/2), so a record wider than the wire drawn (an invisible wall round it) fails. The third 9 mm
    conductor's record widened to 20 mm stands 5.5 mm proud of its 4.5 mm tube and fails naming the wire; widened to
    18 mm it stands 4.5 mm proud, inside its 5 mm, and passes. The built spans reach their radius (the drop's cores
    0.243 mm beyond its 7.5 mm, so the record stands nowhere beyond its tube)."""
    _, built, data = build(repo_root, "L03_medium")
    for diameter, fails in ((0.020, True), (0.018, False)):
        wide = copy.deepcopy(data)
        wide["wires"][2]["diameter_m"] = diameter
        problems, lines = audit.run(wide, built["lods"][0])
        line = next(q for q in lines if q.startswith("audit wires[2] "))
        print(line)
        assert f"its tube reaches 4.500 mm of the record's {diameter * 500:g} mm radius, tolerance 5 mm" in line
        assert problems == ([f"wires[2] {diameter * 1000:g} mm (slot aluminium): its tube reaches 4.500 mm of the "
                             "record's 10 mm radius, 5.500 mm short, more than the tolerance 5 mm: the record is "
                             "wider than the wire drawn"] if fails else [])
    for name, reach in (("L03_thin", "2.500 mm of the record's 2.5 mm"), ("L03_thick", "7.743 mm of the record's 7.5 mm")):
        _, built, data = build(repo_root, name)
        problems, lines = audit.run(data, built["lods"][0])
        assert problems == [] and f"its tube reaches {reach} radius" in lines[0], lines
        print(lines[0])


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
    """L03_thick: the sheet's 2 x 16 mm² SIP drop, 15 mm (two 7.5 mm cores 3.75 mm off the axis, twisted at
    0.45 m) from L01_straight's hook_2 at 6.355 m to a house at 3.5 m over 20 m; the audit holds both cores to the
    bundle's 7.5 mm envelope; its lowest point is the house end, over the 2.75 m a drop keeps at the house entry. Its
    15 mm is over the TF-20 groove's 14 mm: it waits for a clamp port (sip_bracket, not built)."""
    p, built, data = build(repo_root, "L03_thick")
    assert meta.validate(data, material_ids) == []
    lines, core_r = wire_span.twisted_cores([(0.0, 5.0, 0.0), (1.0, 5.0, 0.0)], 0.020, 2, 0.45, 0.0)
    assert core_r == pytest.approx(0.005) and math.dist(lines[0][0], (0.0, 5.0, 0.0)) == pytest.approx(0.005)
    assert math.dist(lines[0][0], lines[1][0]) == pytest.approx(0.010)
    wire = data["wires"][0]
    assert wire["slot"] == "insulated" and wire["diameter_m"] == 0.015 and len(wire["points"]) == 41
    assert wire["points"][0] == pytest.approx([0.0, 6.355, 0.255842 + 0.0075], abs=1e-6)
    assert wire["diameter_m"] > pole.GROOVE_W_M  # no TF-20 port seats it (task 1.5's reading §1)
    assert min(q[1] for q in wire["points"]) == 3.5 >= wire_span.CLEARANCE_M["sip_drop"]
    problems, report = audit.run(data, built["lods"][0])
    print(report[0])
    assert problems == [] and report[0].startswith("audit wires[0] 15 mm (slot insulated): largest deviation 0.")
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
    """LOD0 6-sided tubes, LOD1 4-sided from 20 m, culled where the line's column sum 935 D / d falls under 1 % of a
    pixel (5 mm: 467.5 m, 9 mm: 841.5 m, 15 mm: 1 402.5 m); per conductor a cable `wires` record and an `edge` snag
    along the same points with the wire's radius; no collision, gap, wind volume or port."""
    for name, cull in zip(VARIANTS, (467.5, 841.5, 1402.5)):
        _, built, data = build(repo_root, name)
        assert [lod["switch_distance_m"] for lod in data["lods"]] == [0.0, 20.0]
        assert data["cull_distance_m"] == pytest.approx(cull, abs=1e-6)
        assert 935 * data["wires"][0]["diameter_m"] / data["cull_distance_m"] == pytest.approx(0.01)
        assert data["collision"] == data["gaps"] == data["wind_volumes"] == data["ports"] == []
        assert [m["physics"] for m in data["materials"]] == ["cable"]
        assert len(data["snag"]) == len(data["wires"])
        edge = {"L03_thin": None, "L03_medium": 0.0015, "L03_thick": 0.00375}[name]  # solid, D / 6, the core
        for wire, snag in zip(data["wires"], data["snag"]):
            want = {"kind": "edge", "points": wire["points"], "radius_m": wire["diameter_m"] / 2}
            want.update({"edge_radius_m": edge} if edge else {})
            assert snag == dict(want, material="cable")
            assert list(snag) == ["kind", "points", "radius_m"] + (["edge_radius_m"] if edge else []) + ["material"]
            assert wire["material"] == "cable" and wire["damage"] == "intact"
        tris = [lod["tris"] for lod in data["lods"]]
        assert tris[0] > tris[1]
        print(f"{name}: triangles per LOD {tris}")
    assert [build(repo_root, n)[2]["lods"][0]["tris"] for n in VARIANTS] == [968, 3872, 8656]
    assert [build(repo_root, n)[2]["lods"][1]["tris"] for n in VARIANTS] == [644, 2576, 324]


def test_records_carry_the_conductors_stiffness_and_breaking_force(repo_root, material_ids):
    """Task 1.5's reading §2.2 and §8 item 1: every record carries EA and the rated breaking force from the
    per-type table: 5 mm solid steel 200 GPa over its whole 19.63 mm² and ГОСТ 3282's 390 N/mm² floor (3.93 MN,
    7 658 N); 9 mm A-50 63 GPa over its published 49.5 mm² and its published 8 198 N (3.12 MN); a SIP drop 63 GPa over
    its two 16 mm² cores at A-50's 165.6 N/mm² (assumed: 2.02 MN, 5 300 N). A type without a row is refused."""
    want = {"L03_thin": (3.927e6, 7657.6), "L03_medium": (3.1185e6, 8198.0), "L03_thick": (2.016e6, 5299.7)}
    for name, (stiffness, strength) in want.items():
        _, _, data = build(repo_root, name)
        assert meta.validate(data, material_ids) == []
        for wire in data["wires"]:
            assert wire["axial_stiffness_n"] == pytest.approx(stiffness, rel=1e-4)
            assert wire["breaking_force_n"] == pytest.approx(strength, abs=0.1)
            assert list(wire) == list(meta.WIRE_KEYS)
        print(f"{name}: EA {data['wires'][0]['axial_stiffness_n'] / 1e6:.4f} MN, breaking force "
              f"{data['wires'][0]['breaking_force_n']:.1f} N")
    assert wire_span.mechanics(load(repo_root, "L03_medium"))[1] == 8198.0  # A-50 exactly, its own published figure
    for kind in ("acsr", "insulated"):
        problems = params.validate(wire_span, dict(load(repo_root, "L03_medium"), conductor_type=kind))
        assert f"'conductor_type' = '{kind}' has no axial stiffness or breaking force read yet; use one of " \
               "steel_wire, bare_al, sip_drop" in problems, problems


def test_build_is_deterministic_and_the_seed_moves_only_uvs_and_the_twist(repo_root):
    p = load(repo_root, "L03_medium")
    a, b, c = wire_span.build(p), wire_span.build(copy.deepcopy(p)), wire_span.build(dict(p, seed=7))
    assert a["lods"][0].vertices == b["lods"][0].vertices and a["lods"][0].faces == b["lods"][0].faces
    assert a["lods"][0].vertices == c["lods"][0].vertices and a["wires"] == c["wires"]
    assert [f[2] for f in a["lods"][0].faces] != [f[2] for f in c["lods"][0].faces]
    thick = load(repo_root, "L03_thick")
    assert wire_span.build(thick)["lods"][0].vertices != wire_span.build(dict(thick, seed=7))["lods"][0].vertices
