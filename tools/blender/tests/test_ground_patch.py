"""generators.ground_patch with the S04 params (p1-s04-belt-straw, design decisions 1, 4, 6, 8): the slab's top is
the mean at y = 0 with the relief inside +-relief_m, the mounds' spheres sit within 5 cm of their raised cells and
the box top at the mean, the micro mesh holds the kit's scatter at density x area within 5 %, the metadata lists
it, and `check` refuses a bake radius beyond the patch and a mound the relief or the grid cannot carry."""
import copy
import math

import pytest

from generators import ground_patch
from lib import meta, params
from lib.audit import TOLERANCE_M


@pytest.fixture(scope="module")
def built(s04_params):
    return ground_patch.build(s04_params)


def test_s04_params_conform_and_defaults_are_s04(s04_params):
    assert params.validate(ground_patch, s04_params) == []
    flat = params._flatten(s04_params)
    assert set(flat) == set(ground_patch.PARAMS)
    for key, spec in ground_patch.PARAMS.items():
        assert spec["default"] == flat[key], key
        assert spec["effect"] and spec["type"] in params.TYPES
        if spec["type"] in ("int", "float"):
            assert spec["range"][0] <= spec["default"] <= spec["range"][1], key


def test_slab_top_is_the_mean_with_the_relief_inside_its_range(built, s04_params):
    size, relief = s04_params["size_m"], s04_params["relief_m"]
    lod0, lod1, lod2 = built["lods"]
    assert lod0 is lod1, "LOD1 is the grid alone"
    lo, hi = lod0.bounds()
    assert lo == pytest.approx((-size / 2, -ground_patch.SLAB_M, -size / 2))
    assert hi[0] == pytest.approx(size / 2) and hi[2] == pytest.approx(size / 2)
    mounds = [c for c in built["collision"] if c["shape"] == "sphere"]
    n = round(size / s04_params["cell_m"])
    grid = []
    for x, y, z in lod0.vertices:
        if y <= -ground_patch.SLAB_M + 1e-9:
            continue
        under_mound = any(math.hypot(x - m["center"][0], z - m["center"][2]) < 0.5 for m in mounds)
        on_grid = abs((x + size / 2) / (size / n) - round((x + size / 2) / (size / n))) < 1e-9 and \
            abs((z + size / 2) / (size / n) - round((z + size / 2) / (size / n))) < 1e-9
        if not under_mound and on_grid:
            grid.append(y)
    assert len(grid) > 0.9 * (n + 1) ** 2
    assert max(abs(y) for y in grid) <= relief + 1e-9 and max(abs(y) for y in grid) > 0.9 * relief
    assert sum(grid) / len(grid) == pytest.approx(0.0, abs=0.0005), "the box top at the mean"
    box = built["collision"][0]
    assert box["shape"] == "box" and box["center"] == [0.0, -ground_patch.SLAB_M / 2, 0.0] and box["landable"]
    assert box["size"] == [size, ground_patch.SLAB_M, size] and box["material"] == "surface:belt_straw"
    assert lod2.triangles() == 2 * 4 + 4 * 2 * 2 + 2 and lod2.bounds()[1][1] == 0.0  # a 2 x 2 top, the walls, the bottom


def test_mound_spheres_sit_within_5cm_of_their_raised_cells(built, s04_params):
    mounds = [c for c in built["collision"] if c["shape"] == "sphere"]
    assert len(mounds) == round(s04_params["mounds_per_m2"] * s04_params["size_m"] ** 2) == 3
    lod0 = built["lods"][0]
    for m in mounds:
        assert m["material"] == "surface:crater_spoil" and m["landable"]
        cx, cy, cz, r = m["center"][0], m["center"][1], m["center"][2], m["radius"]
        assert cy - r >= -ground_patch.SLAB_M - 1e-9, "the sphere stays inside the slab"
        apex = cy + r
        assert s04_params["mound_h_m"]["min"] <= apex <= s04_params["mound_h_m"]["max"]
        raised = [(x, y, z) for x, y, z in lod0.vertices if y > 0 and math.hypot(x - cx, z - cz) < r]
        assert raised, "a mound raises vertices"
        assert max(y for _, y, _ in raised) == pytest.approx(apex, abs=TOLERANCE_M)
        for x, y, z in raised:
            to_sphere = abs(math.sqrt((x - cx) ** 2 + (y - cy) ** 2 + (z - cz) ** 2) - r)
            assert min(to_sphere, abs(y)) <= TOLERANCE_M, f"vertex {(x, y, z)} is off the sphere and the slab top"


def test_micro_mesh_holds_the_scatter_at_density_times_area(built, s04_params):
    micro = built["micro"]
    assert len(micro) == 1 and micro[0]["lod"] == 0 and micro[0]["kind"] == "straw"
    area = math.pi * s04_params["bake_radius_m"] ** 2
    expected = area * (s04_params["straw_per_m2"] + s04_params["stem_per_m2"] + s04_params["twig_per_m2"])
    assert abs(micro[0]["count"] / expected - 1) <= 0.05, f"{micro[0]['count']} instances for {expected:.0f}"
    mesh = micro[0]["mesh"]
    assert mesh.triangles() < 450_000 and mesh.slots() == ["straw"]
    lo, hi = mesh.bounds()
    reach = s04_params["bake_radius_m"] + s04_params["straw_len_m"]["max"] / 2
    assert -reach <= lo[0] and hi[0] <= reach and -reach <= lo[2] and hi[2] <= reach
    assert hi[1] >= s04_params["stem_h_m"]["max"] * 0.9 and lo[1] >= -ground_patch.SLAB_M, "buried ends stay inside the slab"


def test_metadata_lists_the_micro_mesh_and_only_the_slab_shapes(built, s04_params, material_ids):
    data = meta.assemble("S04", "S04_patch", "ground_patch", s04_params, built, {"ground": "belt_straw", "straw": "straw"})
    assert meta.validate(data, material_ids) == []
    assert data["micro"] == [{"lod": 0, "name": "S04_patch_LOD0_straw", "kind": "straw", "count": built["micro"][0]["count"],
                              "tris": built["micro"][0]["mesh"].triangles()}]
    assert [lod["mesh"] for lod in data["lods"]] == ["S04_patch_LOD0", "S04_patch_LOD1", "S04_patch_LOD2"]
    assert [lod["switch_distance_m"] for lod in data["lods"]] == [0.0, 8.0, 60.0] and data["cull_distance_m"] == 400.0
    assert data["bounds"]["max"][1] > 1.0, "the standing stems count towards the bounds"
    assert data["gaps"] == [] and data["wind_volumes"] == [] and data["snag"] == []
    assert [m["physics"] for m in data["materials"]] == ["surface:belt_straw"] * 2


@pytest.mark.parametrize("edit, expected", [
    (lambda p: p.update(bake_radius_m=7.0), "'bake_radius_m' = 7.0 reaches beyond the patch; at most size_m / 2 = 6"),
    (lambda p: p["mound_h_m"].update(min=0.03), "'mound_h_m.min' = 0.03 must rise above 'relief_m' = 0.04"),
    (lambda p: p.update(cell_m=0.3), "'cell_m' = 0.3 cannot follow the mound spheres"),
    (lambda p: p["mound_w_m"].update(min=0.6), "'mound_w_m.min' = 0.6 exceeds 'mound_w_m.max' = 0.5"),
    (lambda p: p["lod"].update(lod2_switch_m=5.0), "'lod.lod1_switch_m' < 'lod.lod2_switch_m' < 'lod.cull_m' must hold"),
])
def test_check_refuses_naming_the_rule(s04_params, edit, expected):
    edited = copy.deepcopy(s04_params)
    edit(edited)
    problems = params.validate(ground_patch, edited)
    assert len(problems) == 1 and problems[0].startswith(expected), problems


def test_seed_moves_the_relief_and_the_scatter_but_not_the_bounds(built, s04_params):
    other = ground_patch.build(dict(s04_params, seed=2))
    assert other["lods"][0].vertices != built["lods"][0].vertices
    assert other["micro"][0]["mesh"].vertices != built["micro"][0]["mesh"].vertices
    assert other["lods"][0].bounds()[0] == built["lods"][0].bounds()[0]
    assert ground_patch.build(s04_params)["micro"][0]["mesh"].vertices == built["micro"][0]["mesh"].vertices
