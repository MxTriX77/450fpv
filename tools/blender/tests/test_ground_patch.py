"""generators.ground_patch with the S04 params (p1-s04-belt-straw, design decisions 1, 4, 6, 8): the slab's top is
the mean at y = 0 with the relief inside +-relief_m, the mounds' spheres sit within 5 cm of their raised cells and
the box top at the mean, a mound's top is bare soil, the micro mesh holds the kit's scatter at density x the
feathered bake area within 5 %, the feather thins the rim, the metadata lists it, and `check` refuses a bake radius
beyond the patch, a feather wider than the bake, and a mound the relief or the grid cannot carry."""
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


def feathered_area(radius, feather):
    """The bake's area weighted by the kept share: the full disc inside radius - feather, then the smoothstep ring
    (the integral of 2 pi r s((R - r) / f) over the ring, with the integrals of s and u s over [0, 1] at 1/2 and 7/20)."""
    return math.pi * (radius - feather) ** 2 + 2 * math.pi * feather * (radius * 0.5 - feather * 0.35)


def test_micro_mesh_holds_the_scatter_at_density_times_area(built, s04_params):
    micro = built["micro"]
    assert len(micro) == 1 and micro[0]["lod"] == 0 and micro[0]["kind"] == "straw"
    area = feathered_area(s04_params["bake_radius_m"], s04_params["bake_feather_m"])
    expected = area * (s04_params["straw_per_m2"] + s04_params["stem_per_m2"] + s04_params["twig_per_m2"])
    assert abs(micro[0]["count"] / expected - 1) <= 0.05, f"{micro[0]['count']} instances for {expected:.0f}"
    mesh = micro[0]["mesh"]
    assert mesh.triangles() < 450_000 and mesh.slots() == ["straw"]
    lo, hi = mesh.bounds()
    # a lying straw reaches half its length past its scatter point, a leaning stem its tuft radius plus its height
    reach = s04_params["bake_radius_m"] + max(s04_params["straw_len_m"]["max"] / 2,
                                              ground_patch.TUFT_R_M + s04_params["stem_h_m"]["max"])
    assert -reach <= lo[0] and hi[0] <= reach and -reach <= lo[2] and hi[2] <= reach
    assert hi[1] >= s04_params["stem_h_m"]["max"] * 0.9 and lo[1] >= -ground_patch.SLAB_M, "buried ends stay inside the slab"


def test_metadata_lists_the_micro_mesh_and_only_the_slab_shapes(built, s04_params, material_ids):
    data = meta.assemble("S04", "S04_patch", "ground_patch", s04_params, built,
                         {"ground": "belt_straw", "soil": "belt_soil", "straw": "straw"})
    assert meta.validate(data, material_ids) == []
    assert data["micro"] == [{"lod": 0, "name": "S04_patch_LOD0_straw", "kind": "straw", "count": built["micro"][0]["count"],
                              "tris": built["micro"][0]["mesh"].triangles()}]
    assert [lod["mesh"] for lod in data["lods"]] == ["S04_patch_LOD0", "S04_patch_LOD1", "S04_patch_LOD2"]
    assert [lod["switch_distance_m"] for lod in data["lods"]] == [0.0, 8.0, 60.0] and data["cull_distance_m"] == 400.0
    assert data["bounds"]["max"][1] > 1.0, "the standing stems count towards the bounds"
    assert data["gaps"] == [] and data["wind_volumes"] == [] and data["snag"] == []
    assert [(m["slot"], m["physics"]) for m in data["materials"]] == [
        ("ground", "surface:belt_straw"), ("soil", "surface:crater_spoil"), ("straw", "surface:belt_straw")]


@pytest.mark.parametrize("edit, expected", [
    (lambda p: p.update(bake_radius_m=7.0), "'bake_radius_m' = 7.0 reaches beyond the patch; at most size_m / 2 = 6"),
    (lambda p: p.update(bake_feather_m=4.5), "'bake_feather_m' = 4.5 exceeds 'bake_radius_m' = 4.0"),
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


def test_mound_tops_are_bare_soil_and_the_rest_is_the_mat(built):
    mounds = [c for c in built["collision"] if c["shape"] == "sphere"]
    lod0, _, lod2 = built["lods"]
    soil = []
    for indices, _, _, slot in lod0.faces:
        centroid = [sum(lod0.vertices[i][k] for i in indices) / len(indices) for k in (0, 2)]
        under = [m for m in mounds if math.hypot(centroid[0] - m["center"][0], centroid[1] - m["center"][2]) < 0.25]
        if slot == ground_patch.SOIL_SLOT:
            soil.append(centroid)
            assert under, f"a soil face at {centroid} lies on no mound"
        else:
            assert slot == ground_patch.SLOT
    for m in mounds:
        on = [c for c in soil if math.hypot(c[0] - m["center"][0], c[1] - m["center"][2]) < 0.25]
        assert len(on) >= 20, f"the mound at {m['center']} has {len(on)} soil faces"
    assert lod2.slots() == [ground_patch.SLOT], "the flat far LOD has no mounds"


def test_feather_thins_the_bake_rim(s04_params):
    """Short lying straws only, so each instance sits at its scatter point: with the feather the outer 0.3 m of the
    bake holds a small share of the inner density, without it about the same, and nothing lies past the radius."""
    def density(feather):
        p = copy.deepcopy(s04_params)
        p.update(stem_per_m2=0.0, twig_per_m2=0.0, bake_feather_m=feather, straw_len_m={"min": 0.05, "max": 0.06})
        mesh = ground_patch.build(p)["micro"][0]["mesh"]
        radius = p["bake_radius_m"]
        r = [math.hypot(x, z) for x, _, z in mesh.vertices]
        assert max(r) <= radius + 0.04
        inner = sum(1 for v in r if v < 2.5) / (math.pi * 2.5 ** 2)
        rim = sum(1 for v in r if radius - 0.3 <= v < radius - 0.03) / (math.pi * ((radius - 0.03) ** 2 - (radius - 0.3) ** 2))
        return rim / inner

    assert density(1.0) < 0.15, "the feathered rim thins out"
    assert density(0.0) > 0.85, "without a feather the rim is as dense as the middle"
