"""lib.params against generators.debris_brick.PARAMS: the committed K01 params conform, every declared default
is in range, and an out-of-range value, an unknown key, a missing key, a wrong type or a chamfer that cannot fit
is refused naming the key and its range (p1-pipeline-hardening, "Parameter sanity")."""
import copy

import pytest

from generators import debris_brick
from lib import params


def test_k01_params_conform(k01_params):
    assert params.validate(debris_brick, k01_params) == []


def test_defaults_are_k01_and_in_range(k01_params):
    flat = params._flatten(k01_params)
    assert set(flat) == set(debris_brick.PARAMS)
    for key, spec in debris_brick.PARAMS.items():
        assert spec["default"] == flat[key], key
        assert spec["effect"] and spec["type"] in params.TYPES
        if spec["type"] in ("int", "float"):
            assert spec["range"][0] <= spec["default"] <= spec["range"][1], key


def test_out_of_range_bevel_is_refused_with_its_range(k01_params):
    edited = copy.deepcopy(k01_params)
    edited["bevel_mm"] = {"min": 200, "max": 200}
    assert params.validate(debris_brick, edited) == [
        "'bevel_mm.min' = 200 is outside [0.0, 10.0]",
        "'bevel_mm.max' = 200 is outside [0.0, 10.0]",
    ]


@pytest.mark.parametrize("edit, expected", [
    (lambda p: p.update(bevl_mm=p.pop("bevel_mm")), ["unknown key 'bevl_mm.min' (debris_brick knows: seed, ",
                                                     "unknown key 'bevl_mm.max' (",
                                                     "missing key 'bevel_mm.min' (float, [0.0, 10.0])",
                                                     "missing key 'bevel_mm.max' (float, [0.0, 10.0])"]),
    (lambda p: p.update(colour="red"), ["unknown key 'colour' (debris_brick knows: seed, size_m.length, "]),
    (lambda p: p.pop("lod"), ["missing key 'lod.lod1_switch_m' (float, [0.1, 100.0])",
                              "missing key 'lod.cull_m' (float, [1.0, 2000.0])"]),
    (lambda p: p.update(seed=1.5), ["'seed' must be int, got 1.5"]),
    (lambda p: p.update(seed=True), ["'seed' must be int, got True"]),
    (lambda p: p.update(physics_material=7), ["'physics_material' must be str, got 7"]),
    (lambda p: p["size_m"].update(height=0.005), ["'size_m.height' = 0.005 is outside [0.02, 1.0]"]),
    (lambda p: p["bevel_mm"].update(min=4.0), ["'bevel_mm.min' = 4.0 exceeds 'bevel_mm.max' = 3.0"]),
    (lambda p: p["bevel_mm"].update(max=10.0) or p["size_m"].update(height=0.02),
     ["'bevel_mm.max' = 10.0 must stay under half the smallest size, 10 mm"]),
])
def test_refusals_name_the_key_and_its_range(k01_params, edit, expected):
    edited = copy.deepcopy(k01_params)
    edit(edited)
    problems = params.validate(debris_brick, edited)
    assert len(problems) == len(expected), problems
    for problem, want in zip(problems, expected):
        assert problem.startswith(want), problems
