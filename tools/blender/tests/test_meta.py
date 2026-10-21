"""lib.meta and lib.catalog: schema-1 validation, the conventions of design decision 6 (physics review R2), the
closed physics-material list (R3), the deterministic writer, params_sha256, and the committed K01 metadata against
the committed params."""
import copy
import json

import pytest

from generators import debris_brick
from lib import catalog, meta

VISUALS = {"brick": "brick_red"}
# Review R3's eight rows, the nine signed off in p1-pipeline-hardening/review-materials.md and the two of the
# fortifications physics reading (p1-fortifications-sheet review-physics §4).
REVIEW_MATERIALS = {"steel", "sheet_metal", "timber", "masonry", "concrete", "glass", "cable", "foliage",
                    "bark", "twig", "deadwood", "asbestos_cement", "straw", "fabric", "rubber", "plastic", "mesh",
                    "razor_wire", "sandbag"}
WIRE_FIELDS = "slot, diameter_m, mass_kg_per_m, sag_m, axial_stiffness_n, breaking_force_n, material, damage, points"
WIRE_POSITIVE = "diameter_m, mass_kg_per_m, axial_stiffness_n and breaking_force_n must be positive and sag_m at least 0"
SURFACE_RULE = "is not one of the thirteen surface ids surface:{meadow_sod, dry_crust, crater_spoil, belt_straw, " \
               "belt_bare, tilled, burnt_field, weeds, yard_litter, rubble, asphalt, concrete_slab, dirt_track}"


def k01_meta(params):
    return meta.assemble("K01", "K01_whole", "debris_brick", params, debris_brick.build(params), VISUALS)


def with_physics_features(data):
    """K01's metadata plus one valid capsule, gap, port and wire and two snags (the second a wire's, with its edge
    radius), so every convention rule has something to check."""
    data["collision"].append({"shape": "capsule", "center": [0.0, 0.5, 0.0], "radius": 0.05, "height": 1.0,
                              "rotation": [0.0, 0.0, 0.0, 1.0], "material": "steel", "landable": False})
    data["gaps"].append({"id": "door", "polygon": [[0.0, 0.0, 0.0], [0.9, 0.0, 0.0], [0.9, 2.0, 0.0], [0.0, 2.0, 0.0]],
                         "normal": [0.0, 0.0, 1.0], "narrowest_m": 0.9})
    data["snag"].append({"kind": "edge", "points": [[0.0, 2.0, 0.0], [0.9, 2.0, 0.0]], "radius_m": 0.002})
    data["snag"].append({"kind": "edge", "points": [[0.0, 7.0, 0.3], [40.0, 7.0, 0.3]], "radius_m": 0.0045,
                         "edge_radius_m": 0.0015, "material": "cable"})
    data["ports"].append({"id": "hook_0", "position": [0.0, 7.0, 0.3], "direction": [0.0, 0.0, 1.0],
                          "max_diameter_m": 0.014})
    data["materials"].append({"slot": "wire", "visual": "wire_steel", "physics": "cable"})
    data["wires"].append({"slot": "wire", "diameter_m": 0.005, "mass_kg_per_m": 0.154, "sag_m": 0.8,
                          "axial_stiffness_n": 3.93e6, "breaking_force_n": 7660.0, "material": "cable",
                          "damage": "intact", "points": [[0.0, 7.0, 0.3], [20.0, 6.2, 0.3], [40.0, 7.0, 0.3]]})
    return data


def test_assembled_metadata_validates_in_schema_order(k01_params, material_ids):
    data = k01_meta(k01_params)
    assert meta.validate(data, material_ids) == []
    assert list(data) == list(meta.KEY_ORDER)
    assert data["units"] == "m" and data["frame"] == "unity-lh-yup" and data["schema"] == 1
    assert data["bounds"] == {"min": [-0.125, 0.0, -0.06], "max": [0.125, 0.065, 0.06]}
    assert [lod["tris"] for lod in data["lods"]] == [44, 12]
    assert data["micro"] == []
    assert data["materials"] == [{"slot": "brick", "visual": "brick_red", "physics": "masonry"}]
    assert data["gaps"] == [] and data["wind_volumes"] == [] and data["snag"] == [] and data["ports"] == []
    assert data["wires"] == []


def test_micro_meshes_are_named_counted_and_bounded(k01_params, material_ids):
    """p1-s04-belt-straw, "Visual-only micro-detail meshes": a generator's micro mesh is listed under `micro` by its
    LOD, kind and count, named <variant>_LOD<n>_<kind>, and its vertices widen the bounds; a bad entry is refused."""
    from lib.geometry import Mesh
    built = debris_brick.build(k01_params)
    tuft = Mesh()
    for p in ((0.0, 0.0, 0.0), (0.01, 0.0, 0.0), (0.0, 0.3, 0.0)):
        tuft.vertex(p)
    tuft.face([0, 1, 2], (0.0, 0.0, 1.0), "straw")
    built["micro"] = [{"lod": 0, "kind": "straw", "count": 7, "mesh": tuft}]
    data = meta.assemble("K01", "K01_whole", "debris_brick", k01_params, built, dict(VISUALS, straw="straw"))
    assert meta.validate(data, material_ids) == []
    assert data["micro"] == [{"lod": 0, "name": "K01_whole_LOD0_straw", "kind": "straw", "count": 7, "tris": 1}]
    assert data["bounds"]["max"][1] == 0.3
    data["micro"][0]["lod"] = 5
    assert any(p.startswith("micro[0]: needs lod, name, kind, count, tris") for p in meta.validate(data, material_ids))
    built["micro"][0]["lod"] = 5
    with pytest.raises(ValueError, match="LOD 5, which does not exist"):
        meta.assemble("K01", "K01_whole", "debris_brick", k01_params, built, dict(VISUALS, straw="straw"))


def test_lod_bounds_are_written_under_the_bounds(k01_params, material_ids):
    """p1-r01-vaz-sedan, QA 2.1's S2: a generator's "lod_bounds", the object's own box without what its meshes lay
    round it, lands as bounds.lod (the LOD group's frame); one outside the bounds, or turned inside out, is refused;
    without it nothing is written."""
    built = debris_brick.build(k01_params)
    built["lod_bounds"] = ((-0.1, 0.0, -0.05), (0.1, 0.06, 0.05))
    data = meta.assemble("K01", "K01_whole", "debris_brick", k01_params, built, VISUALS)
    assert data["bounds"]["lod"] == {"min": [-0.1, 0.0, -0.05], "max": [0.1, 0.06, 0.05]}
    assert meta.validate(data, material_ids) == []
    assert json.loads(meta.dumps(data))["bounds"]["lod"] == data["bounds"]["lod"]
    for lo, hi in (([-0.2, 0.0, -0.05], [0.1, 0.06, 0.05]), ([0.1, 0.0, -0.05], [-0.1, 0.06, 0.05])):
        data["bounds"]["lod"] = {"min": lo, "max": hi}
        assert meta.validate(data, material_ids) == ["bounds.lod needs min <= max as two 3-vectors inside the bounds"]
    assert "lod" not in k01_meta(k01_params)["bounds"]


def test_writer_is_deterministic_and_lf_terminated(k01_params):
    text = meta.dumps(k01_meta(k01_params))
    assert text == meta.dumps(k01_meta(json.loads(json.dumps(k01_params))))
    assert text.endswith("}\n") and "\r" not in text
    assert json.loads(text)["bounds"]["min"][1] == 0.0  # no -0.0 and no float noise in the file


def test_params_sha256_ignores_formatting_and_key_order(k01_params):
    reordered = json.loads(json.dumps(dict(reversed(list(k01_params.items()))), indent=4))
    assert catalog.params_sha256(k01_params) == catalog.params_sha256(reordered)
    taller = copy.deepcopy(k01_params)
    taller["size_m"]["height"] = 0.088
    assert catalog.params_sha256(taller) != catalog.params_sha256(k01_params)


def test_catalog_slot_without_visual_is_refused(k01_params):
    with pytest.raises(ValueError, match="slot 'brick'"):
        meta.assemble("K01", "K01_whole", "debris_brick", k01_params, debris_brick.build(k01_params), {})


@pytest.mark.parametrize("edit, expected", [
    (lambda d: d.pop("gaps"), "missing keys: gaps"),
    (lambda d: d.update(frame="blender-rh-zup"), "units/frame"),
    (lambda d: d["collision"][0].update(shape="blob"), "collision[0]: shape must be one of"),
    (lambda d: d["collision"][0].pop("landable"), "boolean landable"),
    (lambda d: d["lods"][0].update(switch_distance_m=1.0), "must start at 0"),
    (lambda d: d.update(cull_distance_m=2.0), "cull_distance_m must lie beyond"),
    (lambda d: d["wind_volumes"].append({"shape": "box", "center": [0, 0, 0], "size": [1, 1, 1],
                                         "rotation": [0, 0, 0, 1], "porosity": 1.5}), "porosity"),
    (lambda d: d["gaps"].append({"id": "g", "polygon": [[0, 0, 0], [1, 0, 0]], "normal": [0, 0, 1],
                                 "narrowest_m": 0.1}), "gaps[0]"),
])
def test_validate_names_the_problem(k01_params, material_ids, edit, expected):
    data = k01_meta(k01_params)
    edit(data)
    problems = meta.validate(data, material_ids)
    assert any(expected in p for p in problems), problems


def test_valid_capsule_gap_snag_port_and_wire_pass(k01_params, material_ids):
    assert meta.validate(with_physics_features(k01_meta(k01_params)), material_ids) == []


def test_wires_are_carried_from_the_generator(k01_params, material_ids):
    """p1-l01-l03-pole-wires, "Ports and wires in the metadata": a generator's wires land under `wires` right after
    `ports`, rounded like every number; without any the key is still written, empty."""
    built = debris_brick.build(k01_params)
    built["wires"] = [{"slot": "brick", "diameter_m": 0.009, "mass_kg_per_m": 0.135, "sag_m": 0.8,
                       "axial_stiffness_n": 3.12e6, "breaking_force_n": 8198.0, "material": "cable",
                       "damage": "intact", "points": [[0.0, 7.0000004, 0.3], [40.0, 7.0, 0.3]]}]
    data = meta.assemble("K01", "K01_whole", "debris_brick", k01_params, built, VISUALS)
    assert data["wires"][0]["points"] == [[0.0, 7.0, 0.3], [40.0, 7.0, 0.3]]
    assert list(data).index("wires") == list(data).index("ports") + 1
    assert meta.validate(data, material_ids) == ["wires[0]: slot 'brick' must be a material slot of physics 'cable'"]
    assert '"wires": []' in meta.dumps(k01_meta(k01_params))


def test_thin_wire_lods_are_named_by_the_generator(k01_params, material_ids):
    """p1-l01-l03-pole-wires review S2: each LOD entry says whether the importer draws it with the thin-wire material,
    as the generator names it ("thin_wire", one bool per LOD), never inferred from a second UV; a generator that names
    none writes false on every LOD; a missing or non-boolean flag, or one too few, is refused."""
    assert [lod["thin_wire"] for lod in k01_meta(k01_params)["lods"]] == [False, False]
    built = debris_brick.build(k01_params)
    built["thin_wire"] = [False, True]
    data = meta.assemble("K01", "K01_whole", "debris_brick", k01_params, built, VISUALS)
    assert [list(lod) for lod in data["lods"]] == [["mesh", "tris", "switch_distance_m", "thin_wire"]] * 2
    assert [lod["thin_wire"] for lod in data["lods"]] == [False, True] and meta.validate(data, material_ids) == []
    for flag in (None, 1):
        bad = copy.deepcopy(data)
        bad["lods"][1].pop("thin_wire") if flag is None else bad["lods"][1].update(thin_wire=flag)
        assert meta.validate(bad, material_ids) == [
            "lods need at least one entry with mesh, tris, switch_distance_m and a boolean thin_wire"]
    with pytest.raises(ValueError, match="2 LODs but 2 switch distances and 1 thin_wire flags"):
        meta.assemble("K01", "K01_whole", "debris_brick", k01_params, dict(built, thin_wire=[True]), VISUALS)


def test_ports_are_carried_from_the_generator(k01_params, material_ids):
    """p1-l01-l03-pole-wires, "Ports and wires in the metadata": a generator's ports land under `ports` in schema
    order, rounded like every number; without any the key is still written, empty."""
    built = debris_brick.build(k01_params)
    built["ports"] = [{"id": "hook_0", "position": [0.1, 7.0000004, -0.2], "direction": [0.0, 0.0, -1.0],
                       "max_diameter_m": 0.014}]
    data = meta.assemble("K01", "K01_whole", "debris_brick", k01_params, built, VISUALS)
    assert meta.validate(data, material_ids) == []
    assert data["ports"] == [{"id": "hook_0", "position": [0.1, 7.0, -0.2], "direction": [0.0, 0.0, -1.0],
                              "max_diameter_m": 0.014}]
    assert list(data).index("ports") == list(data).index("wind_volumes") + 1
    assert '"ports": []' in meta.dumps(k01_meta(k01_params))


RUN_HOLE = {"id": "run", "centre_line": [[0.0, 0.0, 0.0], [1.0000004, 0.0, 0.5]], "top_width_m": 0.8, "depth_m": 1.5,
            "band_width_m": 2.3}
HOLE_FIELDS = "id, centre_line, top_width_m, depth_m, band_width_m"


def test_holes_are_carried_from_the_generator(k01_params, material_ids):
    """p1-e01-trench, "Holes in the metadata": a generator's holes land under `holes` right after `wires`, rounded
    like every number; without any the key is still written, empty."""
    built = debris_brick.build(k01_params)
    built["holes"] = [dict(RUN_HOLE)]
    data = meta.assemble("K01", "K01_whole", "debris_brick", k01_params, built, VISUALS)
    assert meta.validate(data, material_ids) == []
    assert data["holes"][0]["centre_line"] == [[0.0, 0.0, 0.0], [1.0, 0.0, 0.5]]
    assert list(data).index("holes") == list(data).index("wires") + 1
    assert '"holes": []' in meta.dumps(k01_meta(k01_params))


@pytest.mark.parametrize("edit, expected", [
    # "Hole validated": fewer than two points, or a band narrower than its top width, names the record and the rule
    (lambda h: h[0].update(centre_line=[[0.0, 0.0, 0.0]]), "holes[0] 'run': needs a centre line of at least 2 points, got 1"),
    (lambda h: h[0].update(band_width_m=0.5), "holes[0] 'run': band_width_m 0.5 is narrower than its top_width_m 0.8"),
    (lambda h: h[0].update(centre_line=[[0.0, 0.0], [1.0, 0.5]]), f"holes[0]: needs {HOLE_FIELDS}"),
    (lambda h: h[0].pop("depth_m"), f"holes[0]: needs {HOLE_FIELDS}"),
    (lambda h: h[0].update(depth_m=0.0), "holes[0] 'run': top_width_m and depth_m must be positive"),
    (lambda h: h.append(dict(h[0])), "holes[1] 'run': id is not unique"),
])
def test_holes_validated(k01_params, material_ids, edit, expected):
    data = k01_meta(k01_params)
    data["holes"] = [copy.deepcopy(RUN_HOLE)]
    edit(data["holes"])
    assert meta.validate(data, material_ids) == [next(p for p in meta.validate(data, material_ids))]
    assert meta.validate(data, material_ids)[0].startswith(expected), meta.validate(data, material_ids)


@pytest.mark.parametrize("edit, expected", [
    (lambda d: d["collision"][1].update(rotation=[0.0, 0.0, 0.0, 2.0]),
     "collision[1]: capsule rotation must be a unit quaternion within 1e-06, |q| = 2.000000"),
    (lambda d: d["collision"][0].update(rotation=[0.0, 0.0, 0.7, 0.7]),
     "collision[0]: box rotation must be a unit quaternion"),
    (lambda d: d["collision"][1].update(height=0.09),
     "collision[1]: capsule height 0.09 must be at least 2*radius = 0.1"),
    (lambda d: d["gaps"][0].update(normal=[0.0, 0.0, 2.0]),
     "gaps[0]: normal must be a unit vector within 1e-06, |n| = 2.000000"),
    (lambda d: d["gaps"][0].update(normal=[1.0, 0.0, 0.0]),
     "gaps[0]: normal is not perpendicular to the polygon: edge 0"),
    (lambda d: d["gaps"][0]["polygon"][2].__setitem__(2, 0.02),
     "gaps[0]: polygon must be planar within 10 mm, a vertex lies 15.0 mm off its plane"),
    (lambda d: d["gaps"][0].update(narrowest_m=3.0),
     "gaps[0]: narrowest_m must lie in (0, polygon extent 2.193], got 3.0"),
    (lambda d: d["gaps"][0].update(narrowest_m=0.0),
     "gaps[0]: narrowest_m must lie in (0, polygon extent 2.193], got 0.0"),
    (lambda d: d["snag"][0].update(kind="spike"),
     "snag[0]: kind must be one of edge, hook, got 'spike'"),
    (lambda d: d["snag"][0].update(points=[[0.0, 2.0, 0.0]]),
     "snag[0]: needs at least 2 points, got 1"),
    (lambda d: d["collision"][0].update(material="unobtainium"),
     "collision[0]: material 'unobtainium' is not in assets-src"),
    (lambda d: d["materials"][0].update(physics="unobtainium"),
     "materials[0]: physics material 'unobtainium' is not in assets-src"),
    (lambda d: d["collision"][0].update(material="surface:nonsense"),
     f"collision[0]: surface material 'surface:nonsense' {SURFACE_RULE}"),
    (lambda d: d["materials"][0].update(physics="surface:nonsense"),
     f"materials[0]: physics surface material 'surface:nonsense' {SURFACE_RULE}"),
    (lambda d: d["collision"][1].update(material="surface:stubble"),
     f"collision[1]: surface material 'surface:stubble' {SURFACE_RULE}"),
    (lambda d: d["wind_volumes"].append({"shape": "cylinder", "center": [0.0, 1.0, 0.0], "radius": 0.2, "height": 2.0,
                                         "rotation": [0.0, 0.0, 0.0, 0.0], "porosity": 0.5}),
     "wind_volumes[0]: cylinder rotation must be a unit quaternion"),
    (lambda d: d["ports"][0].update(direction=[0.0, 0.0, 0.9]),
     "ports[0] 'hook_0': direction must be a unit vector within 1e-06, |d| = 0.900000"),
    (lambda d: d["ports"][0].pop("position"),
     "ports[0]: needs id, position, direction"),
    (lambda d: d["ports"][0].update(id=""),
     "ports[0]: needs id, position, direction"),
    (lambda d: d["ports"].append(dict(d["ports"][0])),
     "ports[1]: id 'hook_0' is not unique"),
    (lambda d: d["ports"][0].pop("max_diameter_m"),
     "ports[0]: needs id, position, direction, max_diameter_m"),
    (lambda d: d["ports"][0].update(max_diameter_m=0.0),
     "ports[0] 'hook_0': max_diameter_m must be positive, got 0.0"),
    # task 1.5's reading §6: an edge radius is optional, positive and at most the record's reach
    (lambda d: d["snag"][1].update(edge_radius_m=0.0),
     "snag[1]: edge_radius_m must be positive and at most radius_m 0.0045, got 0.0"),
    (lambda d: d["snag"][1].update(edge_radius_m=0.005),
     "snag[1]: edge_radius_m must be positive and at most radius_m 0.0045, got 0.005"),
    (lambda d: d["snag"][1].update(edge_radius_m="strand"),
     "snag[1]: edge_radius_m must be positive and at most radius_m 0.0045, got 'strand'"),
    # "Wires validated": a material other than cable, or fewer than two points, names the record and the rule
    (lambda d: d["wires"][0].update(material="steel"),
     "wires[0]: material must be 'cable', got 'steel'"),
    (lambda d: d["wires"][0].update(points=[[0.0, 7.0, 0.3]]),
     "wires[0]: needs a point list of at least 2 points, got 1"),
    (lambda d: d["wires"][0].update(points=[[0.0, 7.0], [40.0, 7.0]]),
     f"wires[0]: needs {WIRE_FIELDS}"),
    (lambda d: d["wires"][0].pop("sag_m"),
     f"wires[0]: needs {WIRE_FIELDS}"),
    (lambda d: d["wires"][0].update(diameter_m=0.0),
     f"wires[0]: {WIRE_POSITIVE}"),
    # task 1.5's reading §2.2: the stiffness and the breaking force are required and positive
    (lambda d: d["wires"][0].pop("axial_stiffness_n"),
     f"wires[0]: needs {WIRE_FIELDS}"),
    (lambda d: d["wires"][0].pop("breaking_force_n"),
     f"wires[0]: needs {WIRE_FIELDS}"),
    (lambda d: d["wires"][0].update(axial_stiffness_n=0.0),
     f"wires[0]: {WIRE_POSITIVE}"),
    (lambda d: d["wires"][0].update(breaking_force_n=-8198.0),
     f"wires[0]: {WIRE_POSITIVE}"),
    (lambda d: d["wires"][0].update(damage="frayed"),
     "wires[0]: damage must be one of intact, cut, down, got 'frayed'"),
    (lambda d: d["wires"][0].update(slot="brick"),
     "wires[0]: slot 'brick' must be a material slot of physics 'cable'"),
])
def test_convention_violations_name_the_rule(k01_params, material_ids, edit, expected):
    data = with_physics_features(k01_meta(k01_params))
    edit(data)
    problems = meta.validate(data, material_ids)
    # One rule per edit, except that a vertex off the plane also tilts its edges against the normal.
    assert any(p.startswith(expected) for p in problems), problems
    assert all(p.startswith(expected.split(":")[0]) for p in problems), problems


@pytest.mark.parametrize("surface_id", meta.SURFACE_IDS)
def test_every_surface_id_is_accepted_on_a_shape_and_a_slot(k01_params, material_ids, surface_id):
    """The trench filler scenario: `surface:belt_bare` passes, and so does each of the other twelve names."""
    data = with_physics_features(k01_meta(k01_params))
    data["collision"][1]["material"] = f"surface:{surface_id}"
    data["materials"][0]["physics"] = f"surface:{surface_id}"
    assert meta.validate(data, material_ids) == []


def test_physics_material_table_holds_the_review_rows(material_ids):
    assert material_ids == REVIEW_MATERIALS
    assert not material_ids & set(meta.SURFACE_IDS), "asphalt lives as a surface id only (review-materials.md)"


def write_table(repo_root, tmp_path, edit):
    table = json.loads((repo_root / meta.PHYSICS_MATERIALS).read_text(encoding="utf-8"))
    edit(table["materials"])
    (tmp_path / "assets-src").mkdir()
    (tmp_path / meta.PHYSICS_MATERIALS).write_text(json.dumps(table), encoding="utf-8")


@pytest.mark.parametrize("edit, expected", [
    (lambda rows: rows["timber"].pop("label"),
     "timber needs .* a label in pilot, measured, published, derived, assumed"),
    (lambda rows: rows["deadwood"].update(break_stress_pa=0),
     "deadwood needs .* break_stress_pa positive if present"),
    (lambda rows: rows.update(asphalt=rows["concrete"]),
     "'asphalt' is a surface id; a plain material id never shares a surface's name"),
    (lambda rows: rows.update({"surface:soil": rows["concrete"]}),
     "'surface:soil' is a surface id"),
])
def test_malformed_physics_material_table_is_refused(repo_root, tmp_path, edit, expected):
    write_table(repo_root, tmp_path, edit)
    with pytest.raises(ValueError, match=expected):
        meta.load_physics_materials(tmp_path)


def test_textures_are_carried_from_the_generator(k01_params, material_ids):
    """p1-r01-vaz-sedan, "Per-pane crack masks on a transparent material": a generator's textures land under
    `textures` as <variant>_<name>.png with their role, slot and UV rectangle (the bytes stay out of the file); without
    any the key is still written, empty."""
    assert k01_meta(k01_params)["textures"] == []
    built = debris_brick.build(k01_params)
    built["textures"] = [{"role": "crack_mask", "slot": "brick", "name": "cracks", "uv_min": (0.0, -0.5),
                          "uv_max": (0.4, 0.5), "png": b"\x89PNG"}]
    data = meta.assemble("K01", "K01_whole", "debris_brick", k01_params, built, VISUALS)
    assert data["textures"] == [{"role": "crack_mask", "slot": "brick", "file": "K01_whole_cracks.png",
                                 "uv_min": [0.0, -0.5], "uv_max": [0.4, 0.5]}]
    assert meta.validate(data, material_ids) == []
    assert list(data).index("textures") == list(data).index("holes") + 1


@pytest.mark.parametrize("edit, expected", [
    (lambda t: t.update(role="normal_map"), "role must be one of crack_mask, damage_mask"),
    (lambda t: t.update(slot="glass"), "slot 'glass' is not a material slot"),
    (lambda t: t.update(uv_max=[0.0, 0.5]), "must lie below and left of uv_max"),
    (lambda t: t.update(file="cracks.jpg"), "needs role, slot, file, uv_min, uv_max"),
])
def test_textures_validated(k01_params, material_ids, edit, expected):
    data = k01_meta(k01_params)
    data["textures"] = [{"role": "crack_mask", "slot": "brick", "file": "K01_whole_cracks.png",
                         "uv_min": [0.0, -0.5], "uv_max": [0.4, 0.5]}]
    edit(data["textures"][0])
    problems = meta.validate(data, material_ids)
    assert any(expected in p for p in problems), problems


def test_committed_metadata_matches_committed_params(k01_params, material_ids, k01_out):
    path = k01_out / "K01_whole.assetmeta.json"
    committed = json.loads(path.read_text(encoding="utf-8"))
    assert meta.validate(committed, material_ids) == []
    assert committed["params_sha256"] == catalog.params_sha256(k01_params)
    assert path.read_text(encoding="utf-8") == meta.dumps(k01_meta(k01_params)), "rebuild K01: the metadata is stale"
