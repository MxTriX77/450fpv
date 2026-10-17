"""lib.meta and lib.catalog: schema-1 validation, the deterministic writer, params_sha256, and the committed K01
metadata against the committed params."""
import copy
import json

import pytest

from generators import debris_brick
from lib import catalog, meta

VISUALS = {"brick": "brick_red"}


def k01_meta(params):
    return meta.assemble("K01", "K01_whole", "debris_brick", params, debris_brick.build(params), VISUALS)


def test_assembled_metadata_validates_in_schema_order(k01_params):
    data = k01_meta(k01_params)
    assert meta.validate(data) == []
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
def test_validate_names_the_problem(k01_params, edit, expected):
    data = k01_meta(k01_params)
    edit(data)
    problems = meta.validate(data)
    assert any(expected in p for p in problems), problems


def test_committed_metadata_matches_committed_params(k01_params, k01_out):
    path = k01_out / "K01_whole.assetmeta.json"
    committed = json.loads(path.read_text(encoding="utf-8"))
    assert meta.validate(committed) == []
    assert committed["params_sha256"] == catalog.params_sha256(k01_params)
    assert path.read_text(encoding="utf-8") == meta.dumps(k01_meta(k01_params)), "rebuild K01: the metadata is stale"
