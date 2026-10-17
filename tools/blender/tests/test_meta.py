"""lib.meta and lib.catalog: schema-1 validation, the conventions of design decision 6 (physics review R2), the
closed physics-material list (R3), the deterministic writer, params_sha256, and the committed K01 metadata against
the committed params."""
import copy
import json

import pytest

from generators import debris_brick
from lib import catalog, meta

VISUALS = {"brick": "brick_red"}
REVIEW_MATERIALS = {"steel", "sheet_metal", "timber", "masonry", "concrete", "glass", "cable", "foliage"}


def k01_meta(params):
    return meta.assemble("K01", "K01_whole", "debris_brick", params, debris_brick.build(params), VISUALS)


def with_physics_features(data):
    """K01's metadata plus one valid capsule, gap and snag, so every convention rule has something to check."""
    data["collision"].append({"shape": "capsule", "center": [0.0, 0.5, 0.0], "radius": 0.05, "height": 1.0,
                              "rotation": [0.0, 0.0, 0.0, 1.0], "material": "steel", "landable": False})
    data["gaps"].append({"id": "door", "polygon": [[0.0, 0.0, 0.0], [0.9, 0.0, 0.0], [0.9, 2.0, 0.0], [0.0, 2.0, 0.0]],
                         "normal": [0.0, 0.0, 1.0], "narrowest_m": 0.9})
    data["snag"].append({"kind": "edge", "points": [[0.0, 2.0, 0.0], [0.9, 2.0, 0.0]], "radius_m": 0.002})
    return data


def test_assembled_metadata_validates_in_schema_order(k01_params, material_ids):
    data = k01_meta(k01_params)
    assert meta.validate(data, material_ids) == []
    assert list(data) == list(meta.KEY_ORDER)
    assert data["units"] == "m" and data["frame"] == "unity-lh-yup" and data["schema"] == 1
    assert data["bounds"] == {"min": [-0.125, 0.0, -0.06], "max": [0.125, 0.065, 0.06]}
    assert [lod["tris"] for lod in data["lods"]] == [44, 12]
    assert data["materials"] == [{"slot": "brick", "visual": "brick_red", "physics": "masonry"}]
    assert data["gaps"] == [] and data["wind_volumes"] == [] and data["snag"] == []


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


def test_valid_capsule_gap_and_snag_pass(k01_params, material_ids):
    assert meta.validate(with_physics_features(k01_meta(k01_params)), material_ids) == []


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
    (lambda d: d["collision"][0].update(material="plastic"),
     "collision[0]: material 'plastic' is not in assets-src"),
    (lambda d: d["materials"][0].update(physics="plastic"),
     "materials[0]: physics material 'plastic' is not in assets-src"),
    (lambda d: d["wind_volumes"].append({"shape": "cylinder", "center": [0.0, 1.0, 0.0], "radius": 0.2, "height": 2.0,
                                         "rotation": [0.0, 0.0, 0.0, 0.0], "porosity": 0.5}),
     "wind_volumes[0]: cylinder rotation must be a unit quaternion"),
])
def test_convention_violations_name_the_rule(k01_params, material_ids, edit, expected):
    data = with_physics_features(k01_meta(k01_params))
    edit(data)
    problems = meta.validate(data, material_ids)
    # One rule per edit, except that a vertex off the plane also tilts its edges against the normal.
    assert any(p.startswith(expected) for p in problems), problems
    assert all(p.startswith(expected.split(":")[0]) for p in problems), problems


def test_physics_material_table_holds_the_review_rows(material_ids):
    assert material_ids == REVIEW_MATERIALS


def test_physics_material_table_without_a_label_is_refused(repo_root, tmp_path):
    table = json.loads((repo_root / meta.PHYSICS_MATERIALS).read_text(encoding="utf-8"))
    del table["materials"]["timber"]["label"]
    (tmp_path / "assets-src").mkdir()
    (tmp_path / meta.PHYSICS_MATERIALS).write_text(json.dumps(table), encoding="utf-8")
    with pytest.raises(ValueError, match="timber needs .* a label in pilot, measured, published, derived, assumed"):
        meta.load_physics_materials(tmp_path)


def test_committed_metadata_matches_committed_params(k01_params, material_ids, k01_out):
    path = k01_out / "K01_whole.assetmeta.json"
    committed = json.loads(path.read_text(encoding="utf-8"))
    assert meta.validate(committed, material_ids) == []
    assert committed["params_sha256"] == catalog.params_sha256(k01_params)
    assert path.read_text(encoding="utf-8") == meta.dumps(k01_meta(k01_params)), "rebuild K01: the metadata is stale"
