"""lib.meta and lib.catalog: schema-1 validation, the conventions of design decision 6 (physics review R2), the
closed physics-material list (R3) on shapes, slots and snag records (a record without its member's material, or with
an unknown one, is refused: p1-v01-locust review-physics PR4), the deterministic writer, params_sha256, a variant's
material overrides (p1-v01-locust design decision 7), the micro_elements sidecar the metadata names and hashes and
its malformed records refused by name (p7a-ground 1.5), and the committed K01 metadata against the committed params."""
import copy
import hashlib
import json

import pytest

from generators import debris_brick
from lib import catalog, meta

VISUALS = {"brick": "brick_red"}
# Review R3's eight rows, the nine signed off in p1-pipeline-hardening/review-materials.md and the two of the
# fortifications physics reading (p1-fortifications-sheet review-physics §4), and the drone's carbon (p7b-airframe).
REVIEW_MATERIALS = {"steel", "sheet_metal", "timber", "masonry", "concrete", "glass", "cable", "foliage",
                    "bark", "twig", "deadwood", "asbestos_cement", "straw", "fabric", "rubber", "plastic", "mesh",
                    "razor_wire", "sandbag", "carbon_composite"}
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
    data["snag"].append({"kind": "edge", "points": [[0.0, 2.0, 0.0], [0.9, 2.0, 0.0]], "radius_m": 0.002,
                         "material": "steel"})
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


SHRUB = {"class": "vegetation", "generator": "shrub", "variants": ["V13_mid", "V13_mid_autumn"],
         "materials": {"stem": "bark_shrub", "leaf": "leaf_shrub"}}


def test_a_variant_override_takes_its_slot_and_leaves_the_rest():
    """p1-v01-locust design decision 7, asset-pipeline "Per-variant material overrides": the autumn variant's leaf
    slot takes its own material, its other slots and every other variant keep the asset's."""
    entry = dict(SHRUB, variant_materials={"V13_mid_autumn": {"leaf": "leaf_shrub_autumn"}})
    assert catalog.visuals(entry, "V13_mid_autumn") == {"stem": "bark_shrub", "leaf": "leaf_shrub_autumn"}
    assert catalog.visuals(entry, "V13_mid") == {"stem": "bark_shrub", "leaf": "leaf_shrub"}
    assert catalog.visuals(SHRUB, "V13_mid_autumn") == SHRUB["materials"]


@pytest.mark.parametrize("overrides, expected", [
    ({"V13_tall_autumn": {"leaf": "leaf_shrub_autumn"}}, "names 'V13_tall_autumn', which is not one of its variants"),
    ({"V13_mid_autumn": {"pod": "pod_locust"}}, "names slot 'pod', which V13's materials do not"),
    ({"V13_mid_autumn": {"leaf": ""}}, r"\['leaf'\] must be a material name"),
    ({"V13_mid_autumn": {}}, "must be a non-empty object"),
    (["V13_mid_autumn"], "must be an object"),
])
def test_a_malformed_override_is_refused_named(tmp_path, overrides, expected):
    (tmp_path / "assets-src").mkdir()
    entry = dict(SHRUB, variant_materials=overrides)
    (tmp_path / catalog.CATALOG).write_text(json.dumps({"schema": 1, "assets": {"V13": entry}}), encoding="utf-8")
    with pytest.raises(catalog.CatalogError, match=expected):
        catalog.load(tmp_path)


def test_the_committed_overrides_name_recipes_that_exist(repo_root):
    """Every material a committed variant takes, its overrides included, is a recipe in assets-src/materials."""
    cat = catalog.load(repo_root)
    for asset_id, entry in cat["assets"].items():
        for variant in entry["variants"]:
            for slot, visual in catalog.visuals(entry, variant).items():
                assert (repo_root / catalog.RECIPES / f"{visual}.json").is_file(), f"{variant} {slot}: no recipe {visual}"


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
    (lambda d: d["snag"][0].pop("material"),
     "snag[0]: needs the material of the member it follows"),
    (lambda d: d["snag"][0].update(material="unobtainium"),
     "snag[0]: material 'unobtainium' is not in assets-src"),
    (lambda d: d["snag"][0].update(material=7),
     "snag[0]: needs the material of the member it follows"),
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
    # p7a-ground 1.1, "An area-type row says its law": a copy of straw without area_law, named by row and field.
    (lambda rows: rows["straw"].pop("area_law"), r"straw\.area_law: missing"),
    (lambda rows: rows["fabric"]["area_law"].update(value="sponge"), r"fabric\.area_law\.value: must be one of mat"),
    (lambda rows: rows["sandbag"]["area_reference_depth_m"].pop("label"), r"sandbag\.area_reference_depth_m\.label: missing"),
    (lambda rows: rows["bark"].pop("elastic_modulus_pa"), r"bark\.elastic_modulus_pa: missing"),
    (lambda rows: rows["deadwood"].update(elastic_modulus_pa=7e9), r"deadwood\.elastic_modulus_pa: a value carries its label"),
    (lambda rows: rows["straw"]["wet"]["friction_static"].pop("note"), r"straw\.wet\.friction_static\.note: missing"),
    (lambda rows: rows["straw"]["wet"].update(suction_pa=rows["straw"]["wet"]["adhesion_pa"]),
     r"straw\.wet\.suction_pa: is not a field of the wet block"),
    (lambda rows: rows["concrete"].pop("hardness_pa"), r"concrete\.hardness_pa is missing"),
    (lambda rows: rows["timber"].pop("crush_strength_pa"), r"timber\.crush_strength_pa is missing"),
    (lambda rows: rows["glass"].pop("density_kg_m3"), r"glass\.density_kg_m3 is missing"),
    (lambda rows: rows["steel"]["hardness_pa"].update(label="guessed"), r"steel\.hardness_pa needs a label in pilot"),
    (lambda rows: rows["steel"]["density_kg_m3"].update(note=""), r"steel\.density_kg_m3 needs a note"),
    (lambda rows: rows["rubber"].update(hardness_pa=5.0e6), r"rubber\.hardness_pa must be \{"),
    (lambda rows: rows["timber"]["crush_strength_pa"].update(value=0),
     r"timber\.crush_strength_pa needs a value above 0 or null"),
    (lambda rows: rows["straw"]["density_kg_m3"].update(value=None), r"straw\.density_kg_m3 needs a value above 0$"),
])
def test_malformed_physics_material_table_is_refused(repo_root, tmp_path, edit, expected):
    write_table(repo_root, tmp_path, edit)
    with pytest.raises(ValueError, match=expected):
        meta.load_physics_materials(tmp_path)


def test_area_rows_name_their_law(repo_root):
    """p7a-ground 1.1, asset-pipeline "An area-type row says its law": straw, fabric and sandbag name their law and
    reference depth, each labelled; bark and deadwood carry the modulus their members bend at."""
    rows = json.loads((repo_root / meta.PHYSICS_MATERIALS).read_text(encoding="utf-8"))["materials"]
    assert {r: rows[r]["area_law"]["value"] for r in meta.AREA_LAW_ROWS} == {"straw": "mat", "fabric": "punch",
                                                                             "sandbag": "winkler"}
    for r in meta.AREA_LAW_ROWS:
        assert rows[r]["area_reference_depth_m"]["value"] > 0 and rows[r]["area_law"]["label"] in meta.LABELS
    for r in meta.BENDING_ROWS:
        assert rows[r]["elastic_modulus_pa"]["value"] > 0 and rows[r]["elastic_modulus_pa"]["note"]


def test_surface_ids_are_physics_table(repo_root):
    """The asset side's surface names are physics' surfaces.json's, in its order (surface id i + 1 is SURFACE_IDS[i],
    as MapWorld.SurfaceIds reads them)."""
    table = json.loads((repo_root / "assets-src" / "surfaces.json").read_text(encoding="utf-8"))
    assert tuple(table["surfaces"]) == meta.SURFACE_IDS


def test_every_row_says_what_a_blade_meets(repo_root, material_ids):
    """asset-pipeline (p7f-damage) "Every row says what a blade meets": the three labelled fields on every row (the
    loader refuses the rest, above); the hard rows harder than the blade's hardness_pa in drone.json, the soft ones
    softer (Rabinowicz's rule, p7f-damage design 5); null crush only on the thin compliant rows."""
    rows = json.loads((repo_root / meta.PHYSICS_MATERIALS).read_text(encoding="utf-8"))["materials"]
    drone = json.loads((repo_root / "assets-src" / "drone" / "drone.json").read_text(encoding="utf-8"))
    blade = drone["props"]["blade"]["hardness_pa"]["value"]
    for name, row in rows.items():
        for field in meta.BLADE_FIELDS:
            assert row[field]["label"] in meta.LABELS and row[field]["note"], (name, field)
    for name in ("concrete", "masonry", "steel", "glass"):
        assert rows[name]["hardness_pa"]["value"] > blade, name
    for name in ("timber", "bark", "rubber"):
        assert rows[name]["hardness_pa"]["value"] < blade, name
    passed = {name for name, row in rows.items() if row["crush_strength_pa"]["value"] is None}
    assert passed == {"straw", "foliage", "twig", "fabric", "mesh"}


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


def test_a_texture_may_name_its_channels(k01_params, material_ids):
    """p1-h01-adobe-house, "Generator-written mask atlases": a facade's damage mask names what R, G, B and A hold, in
    that order, after its UV rectangle; a record without channels (R01's masks) is unchanged."""
    built = debris_brick.build(k01_params)
    channels = ["render_depth", "soot", "dirt", "base_band"]
    built["textures"] = [{"role": "damage_mask", "slot": "brick", "name": "damage", "uv_min": (0.0, 0.0),
                          "uv_max": (32.0, 5.0), "channels": channels, "png": b"\x89PNG"}]
    data = meta.assemble("K01", "K01_whole", "debris_brick", k01_params, built, VISUALS)
    assert data["textures"] == [{"role": "damage_mask", "slot": "brick", "file": "K01_whole_damage.png",
                                 "uv_min": [0.0, 0.0], "uv_max": [32.0, 5.0], "channels": channels}]
    assert list(data["textures"][0])[-1] == "channels"
    assert meta.validate(data, material_ids) == []


@pytest.mark.parametrize("edit, expected", [
    (lambda t: t.update(role="normal_map"), "role must be one of crack_mask, damage_mask"),
    (lambda t: t.update(slot="glass"), "slot 'glass' is not a material slot"),
    (lambda t: t.update(uv_max=[0.0, 0.5]), "must lie below and left of uv_max"),
    (lambda t: t.update(file="cracks.jpg"), "needs role, slot, file, uv_min, uv_max"),
    (lambda t: t.update(channels=["soot", "soot"]), "channels must be 1 to 4 distinct non-empty names"),
    (lambda t: t.update(channels=["r", "g", "b", "a", "x"]), "channels must be 1 to 4 distinct non-empty names"),
    (lambda t: t.update(channels=[]), "channels must be 1 to 4 distinct non-empty names"),
    (lambda t: t.update(channels="render_depth"), "channels must be 1 to 4 distinct non-empty names"),
])
def test_textures_validated(k01_params, material_ids, edit, expected):
    data = k01_meta(k01_params)
    data["textures"] = [{"role": "crack_mask", "slot": "brick", "file": "K01_whole_cracks.png",
                         "uv_min": [0.0, -0.5], "uv_max": [0.4, 0.5]}]
    edit(data["textures"][0])
    problems = meta.validate(data, material_ids)
    assert any(expected in p for p in problems), problems


ELEMENTS = [  # a stem rooted in K01's box, a straw lying loose, a twig grown from the box and a leaf from the twig
    {"kind": "stem", "points": [[0.0, 0.0, 0.0], [0.01, 0.2, 0.0], [0.03, 0.5, 0.0]], "diameter_m": 0.004,
     "anchored": True, "parent": -1, "parent_shape": -1},
    {"kind": "straw", "points": [[0.1, 0.07, 0.0], [0.5, 0.07, 0.1]], "diameter_m": 0.003, "anchored": False,
     "parent": -1, "parent_shape": -1},
    {"kind": "twig", "points": [[0.1, 0.065, 0.0], [0.2, 0.2, 0.0], [0.25, 0.3, 0.05]], "diameter_m": 0.006,
     "anchored": True, "parent": -1, "parent_shape": 0},
    {"kind": "leaf", "points": [[0.25, 0.3, 0.05], [0.28, 0.33, 0.05]], "diameter_m": 0.001, "area_m2": 0.0004,
     "anchored": True, "parent": 2, "parent_shape": -1},
]


def elements_meta(params, elements=None):
    """K01's metadata with micro_elements (ELEMENTS unless given) and the records, as build.py holds them."""
    built = debris_brick.build(params)
    built["micro_elements"] = copy.deepcopy(ELEMENTS if elements is None else elements)
    return meta.assemble("K01", "K01_whole", "debris_brick", params, built, VISUALS), built["micro_elements"]


def test_micro_elements_live_in_a_sidecar_the_metadata_names_and_hashes(k01_params, material_ids):
    """p7a-ground 1.5 (asset-pipeline "Asset metadata contract"): a generator's micro_elements are written to
    <variant>.elements.json, one record per line, keys in ELEMENT_KEYS order and rounded as the metadata is; the
    metadata names the file with its SHA-256 and the counts by kind, after the other keys, so any change to an
    element changes the metadata's text, which the world's content hash covers; without elements the key is absent
    and the metadata unchanged."""
    data, elements = elements_meta(k01_params)
    assert meta.validate(data, material_ids, elements) == []
    text = meta.elements_dumps("K01_whole", elements)
    assert data["micro_elements"] == {"file": "K01_whole.elements.json",
                                      "sha256": hashlib.sha256(text.encode("utf-8")).hexdigest(),
                                      "counts": {"straw": 1, "stem": 1, "twig": 1, "leaf": 1}}
    assert list(json.loads(meta.dumps(data)))[-1] == "micro_elements"
    lines = text.split("\n")
    assert lines[0] == '{"schema": 1, "variant": "K01_whole", "units": "m", "frame": "unity-lh-yup", "elements": ['
    assert len(lines) == len(ELEMENTS) + 3 and lines[-2] == "]}" and lines[-1] == ""
    assert json.loads(text)["elements"] == ELEMENTS
    assert list(json.loads(lines[4].rstrip(","))) == [*meta.ELEMENT_KEYS, "area_m2"]
    nudged = copy.deepcopy(ELEMENTS)
    nudged[1]["points"][1][0] += 2e-6
    assert meta.dumps(elements_meta(k01_params, nudged)[0]) != meta.dumps(data), "a moved element moves the metadata"
    rounded = copy.deepcopy(ELEMENTS)
    rounded[1]["points"][1][0] += 4e-7
    assert meta.dumps(elements_meta(k01_params, rounded)[0]) == meta.dumps(data), "below the micrometre it rounds away"
    line = "micro_elements 1 straw, 1 stem, 1 twig, 1 leaf in K01_whole.elements.json"
    assert meta.elements_line(data["micro_elements"]) == line
    assert "micro_elements" not in k01_meta(k01_params)


def edited(index, **fields):
    def edit(elements):
        elements[index].update(fields)
    return edit


@pytest.mark.parametrize("edit, expected", [
    (edited(1, diameter_m=0.0), "micro_elements[1]: diameter_m must be positive, got 0.0"),
    (edited(1, points=[[0.1, 0.07, 0.0], [0.1, 0.07, 0.0]]), "micro_elements[1]: points must be distinct"),
    (edited(0, points=[[0.0, 0.0, 0.0], [0.03, 0.5, 0.0], [0.0000004, 0.0, 0.0]]),
     "micro_elements[0]: points must be distinct"),  # equal once rounded to the micrometre, as written
    (edited(0, kind="grass"), "micro_elements[0]: kind must be one of straw, stem, twig, leaf, got 'grass'"),
    (edited(3, parent=3), "micro_elements[3]: parent must be -1 or the index of an earlier element (below 3), got 3"),
    (edited(3, parent=7), "micro_elements[3]: parent must be -1 or the index of an earlier element (below 3), got 7"),
    (edited(2, parent_shape=1), "micro_elements[2]: parent_shape must be -1 or the index of a collision shape "
                                "(below 1), got 1"),
    (edited(2, parent_shape=-2), "micro_elements[2]: parent_shape must be -1 or the index of a collision shape"),
    (edited(3, parent_shape=0), "micro_elements[3]: parent and parent_shape are both set"),
    (edited(2, anchored=False), "micro_elements[2]: an element grown from a parent is anchored"),
    (edited(1, points=[[0.1, 0.07, 0.0]]), "micro_elements[1]: points must be its centre line as two or three"),
    (edited(1, points=[[0.0, 0.0, 0.0], [0.1, 0.0, 0.0], [0.2, 0.0, 0.0], [0.3, 0.0, 0.0]]),
     "micro_elements[1]: points must be its centre line as two or three"),
    (edited(1, anchored=1), "micro_elements[1]: anchored must be a boolean"),
    (edited(3, area_m2=0.0), "micro_elements[3]: a leaf needs a positive area_m2"),
    (edited(1, area_m2=0.001), "micro_elements[1]: area_m2 belongs to a leaf only"),
    (edited(1, hooks=True), "micro_elements[1]: unknown keys hooks"),
    (lambda e: e[1].pop("anchored"), "micro_elements[1]: needs anchored"),
])
def test_a_malformed_element_is_rejected_naming_the_record_and_the_rule(k01_params, material_ids, edit, expected):
    """asset-pipeline "A malformed element is rejected" (its element part, p7a-ground 1.5)."""
    elements = copy.deepcopy(ELEMENTS)
    edit(elements)
    data, elements = elements_meta(k01_params, elements)
    problems = meta.validate(data, material_ids, elements)
    assert any(expected in p for p in problems), problems


def test_elements_that_are_not_the_named_ones_are_rejected(k01_params, material_ids):
    """The metadata's record holds the sidecar to its records: other records, other counts, another file or none
    given are refused."""
    data, elements = elements_meta(k01_params)
    other = copy.deepcopy(elements)
    other[0]["diameter_m"] = 0.005
    assert meta.validate(data, material_ids, other) == [
        f"micro_elements: sha256 {data['micro_elements']['sha256']} is not the records' "
        f"{meta.elements_record('K01_whole', other)['sha256']}"]
    assert any("counts" in p for p in meta.validate(data, material_ids, elements[:3]))
    assert meta.validate(data, material_ids) == ["micro_elements: the records of K01_whole.elements.json are needed as a "
                                                 "list to validate it"]
    data["micro_elements"]["file"] = "K01.elements.json"
    assert any("file must be 'K01_whole.elements.json'" in p for p in meta.validate(data, material_ids, elements))
    data["micro_elements"]["counts"] = {"grass": 1}
    assert any(p.startswith("micro_elements needs file, sha256") for p in meta.validate(data, material_ids, elements))


def test_committed_metadata_matches_committed_params(k01_params, material_ids, k01_out):
    path = k01_out / "K01_whole.assetmeta.json"
    committed = json.loads(path.read_text(encoding="utf-8"))
    assert meta.validate(committed, material_ids) == []
    assert committed["params_sha256"] == catalog.params_sha256(k01_params)
    assert path.read_text(encoding="utf-8") == meta.dumps(k01_meta(k01_params)), "rebuild K01: the metadata is stale"
