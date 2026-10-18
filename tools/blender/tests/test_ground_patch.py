"""generators.ground_patch with the S04 params (p1-s04-belt-straw, design decisions 1, 4, 6, 8; review-physics PR1-PR8):
the slab's top is the mean at y = 0 with the relief inside +-relief_m, the mounds' spheres sit within 5 cm of their
raised cells, a mound's top is bare soil, the micro mesh holds the kit's scatter at density x the feathered bake
area within 5 %, the feather thins the rim, the metadata lists it, and `check` refuses a bake radius beyond the
patch, a feather wider than the bake, and a mound the relief or the grid cannot carry. The bare patches: the mesh
steps down by the mat depth along each outline to a floor, no shape stands above a floor and the soil box lies
just under it (PR7 a), the old single mat box fails the audit by the step (PR7 b), the shapes come in PR8's order,
the mat boxes are the layout's, and the baked straws over a floor lie on it (N8)."""
import copy
import math
import statistics

import pytest

from generators import ground_patch
from lib import audit, meta, params, patches
from lib.audit import TOLERANCE_M


@pytest.fixture(scope="module")
def built(s04_params):
    return ground_patch.build(s04_params)


@pytest.fixture(scope="module")
def bare(s04_params):
    return patches.layout(**patches.inputs(s04_params))["patches"]


def slab_m(s04_params):
    return s04_params["mat_depth_m"] + ground_patch.SOIL_M


def test_s04_params_conform_and_defaults_are_s04(s04_params):
    assert params.validate(ground_patch, s04_params) == []
    flat = params._flatten(s04_params)
    assert set(flat) == set(ground_patch.PARAMS)
    for key, spec in ground_patch.PARAMS.items():
        assert spec["default"] == flat[key], key
        assert spec["effect"] and spec["type"] in params.TYPES
        if spec["type"] in ("int", "float"):
            assert spec["range"][0] <= spec["default"] <= spec["range"][1], key


def test_slab_top_is_the_mean_with_the_relief_inside_its_range(built, s04_params, bare):
    """The grid's vertices, a floor's lifted back by the mat depth, are the relief: mean 0, within +-relief_m."""
    size, relief, depth = s04_params["size_m"], s04_params["relief_m"], s04_params["mat_depth_m"]
    lod0, lod1, lod2 = built["lods"]
    assert lod0 is lod1, "LOD1 is the grid alone"
    lo, hi = lod0.bounds()
    assert lo == pytest.approx((-size / 2, -slab_m(s04_params), -size / 2))
    assert hi[0] == pytest.approx(size / 2) and hi[2] == pytest.approx(size / 2)
    mounds = [c for c in built["collision"] if c["shape"] == "sphere"]
    n = round(size / s04_params["cell_m"])
    grid = []
    for x, y, z in lod0.vertices:
        if y <= -slab_m(s04_params) + 1e-9:
            continue
        under_mound = any(math.hypot(x - m["center"][0], z - m["center"][2]) < 0.5 for m in mounds)
        on_grid = abs((x + size / 2) / (size / n) - round((x + size / 2) / (size / n))) < 1e-9 and \
            abs((z + size / 2) / (size / n) - round((z + size / 2) / (size / n))) < 1e-9
        if not under_mound and on_grid:
            grid.append(y + depth if patches.inside(bare, x, z) else y)
    assert len(grid) > 0.9 * (n + 1) ** 2
    assert max(abs(y) for y in grid) <= relief + 1e-9 and max(abs(y) for y in grid) > 0.9 * relief
    assert sum(grid) / len(grid) == pytest.approx(0.0, abs=0.0005), "the mat boxes' top at the mean"
    assert lod2.triangles() == 2 * 4 + 4 * 2 * 2 + 2 and lod2.bounds()[1][1] == 0.0  # a 2 x 2 top, the walls, the bottom
    assert lod2.bounds()[0][1] == pytest.approx(-slab_m(s04_params))


def test_bare_patches_step_down_by_the_mat_depth_along_their_outlines(built, s04_params, bare):
    """PR3, spec "Bare patches are ground, not stains": inside every outline the top is the floor, the relief lowered
    by D; along every outline edge stand vertical walls exactly D tall whose widths add up to the outline's
    perimeter; floor and walls take the ground slot."""
    lod0 = built["lods"][0]
    depth, relief = s04_params["mat_depth_m"], s04_params["relief_m"]
    walls = {k: 0.0 for k in range(len(bare))}
    for indices, normal, uvs, slot in lod0.faces:
        points = [lod0.vertices[i] for i in indices]
        if abs(normal[1]) > 1e-9 or len(points) != 4 or min(p[1] for p in points) < -depth - relief - 1e-9 - 0.01:
            continue
        for k, patch in enumerate(bare):
            if all(abs(patches.distance((p[0], p[2]), patch["outline"])) < 1e-9 for p in points):
                tops = sorted(points, key=lambda p: -p[1])[:2]
                walls[k] += math.hypot(tops[0][0] - tops[1][0], tops[0][2] - tops[1][2])
                heights = sorted(p[1] for p in points)
                assert heights[2] - heights[0] == pytest.approx(depth) and heights[3] - heights[1] == pytest.approx(depth)
                assert slot == ground_patch.SLOT
    for k, patch in enumerate(bare):
        perimeter = sum(math.dist(a, b) for a, b in zip(patch["outline"], patch["outline"][1:] + patch["outline"][:1]))
        assert walls[k] == pytest.approx(perimeter, abs=1e-6), f"patch {k}: walls {walls[k]:.4f} m, outline {perimeter:.4f} m"
    floor = 0
    for x, y, z in lod0.vertices:
        if any(patches.distance((x, z), p["outline"]) < -1e-6 for p in bare):
            floor += 1
            assert -depth - relief - 1e-9 <= y <= -depth + relief + 1e-9, (x, y, z)
    assert floor > 100


def test_no_shape_stands_above_a_floor(built, s04_params, bare):
    """PR7 (a), physics' own check, independent of the audit: inside every outline shrunk by 6 cm, sampled every
    2 cm, every shape's signed distance is positive at y = -D + 0.06, -0.03, 0 and +0.05, and the soil box's is
    negative 1 cm under the floor's mean level."""
    depth = s04_params["mat_depth_m"]
    shapes = [audit._Shape(s) for s in built["collision"]]
    reach = [(min(p[0] for t in s.triangles for p in t[:3]), max(p[0] for t in s.triangles for p in t[:3]),
              min(p[2] for t in s.triangles for p in t[:3]), max(p[2] for t in s.triangles for p in t[:3])) for s in shapes]
    samples = 0
    for k, patch in enumerate(bare):
        x0, x1, z0, z1 = patch["window"]
        near = [s for s, r in zip(shapes, reach) if r[0] < x1 and r[1] > x0 and r[2] < z1 and r[3] > z0]
        nx, nz = math.ceil((x1 - x0) / 0.02), math.ceil((z1 - z0) / 0.02)
        for i in range(nx + 1):
            for j in range(nz + 1):
                x, z = x0 + (x1 - x0) * i / nx, z0 + (z1 - z0) * j / nz
                if patches.distance((x, z), patch["outline"]) > -0.06:
                    continue
                samples += 1
                for y in (-depth + 0.06, -0.03, 0.0, 0.05):
                    assert all(s.sdf((x, y, z)) > 0 for s in near), f"patch {k}: a shape stands at {(x, y, z)}"
                assert shapes[0].sdf((x, -depth - 0.01, z)) < 0, f"patch {k}: no soil under {(x, z)}"
    print(f"{samples} floor samples over {len(bare)} patches")
    assert samples > 1000


def test_a_mat_box_left_whole_fails_the_audit_by_the_step(built, s04_params, repo_root):
    """PR7 (b): the same build with the straw boxes replaced by one 12 m straw box over the floors fails box->mesh by
    at least 120 mm (the mat over a visible floor), the direction physics asserts on."""
    size, depth = s04_params["size_m"], s04_params["mat_depth_m"]
    data = meta.assemble("S04", "S04_patch", "ground_patch", s04_params, built,
                         {"ground": "belt_straw", "soil": "belt_soil", "straw": "straw"})
    data["collision"] = [c for c in data["collision"] if c["material"] != "straw"]
    data["collision"].insert(1, {"shape": "box", "center": [0.0, -depth / 2, 0.0], "size": [size, depth, size],
                                 "rotation": [0.0, 0.0, 0.0, 1.0], "material": "straw", "landable": True})
    problems, lines = audit.run(data, built["lods"][0], audit.soft_materials(repo_root))
    line = next(line for line in lines if line.startswith("audit collision[1] box"))
    print(line)
    assert float(line.split("box->mesh ")[1].split(" mm")[0]) >= 120.0
    assert any(p.startswith("collision[1] box: box->mesh ") for p in problems)


def test_shapes_come_soil_fill_edges_mounds(built, s04_params, bare):
    """PR8: collision[0] the soil box one mat depth down and SOIL_M deep, then the axis-aligned fill boxes of
    lib.patches.fill, then each patch's edge boxes in patch order (a box's inner face on its outline edge), then the
    mound spheres; the mat is mat_material from -D to 0, every shape landable."""
    size, depth = s04_params["size_m"], s04_params["mat_depth_m"]
    shapes = built["collision"]
    soil = shapes[0]
    assert soil == {"shape": "box", "center": [0.0, -depth - ground_patch.SOIL_M / 2, 0.0],
                    "size": [size, ground_patch.SOIL_M, size], "rotation": [0.0, 0.0, 0.0, 1.0],
                    "material": "surface:belt_straw", "landable": True}
    fill = patches.fill(size, bare)
    edges = [rect for patch in bare for rect in patch["edges"]]
    assert len(shapes) == 1 + len(fill) + len(edges) + 3
    for shape, (x0, x1, z0, z1) in zip(shapes[1:], fill):
        assert shape["rotation"] == [0.0, 0.0, 0.0, 1.0]
        assert shape["center"] == pytest.approx([(x0 + x1) / 2, -depth / 2, (z0 + z1) / 2])
        assert shape["size"] == pytest.approx([x1 - x0, depth, z1 - z0])
    for shape, rect in zip(shapes[1 + len(fill):], edges):
        box = audit._Shape(shape)
        (ax, az), (dx, dz), (nx, nz) = rect["a"], rect["d"], rect["n"]
        length = rect["t"][1] - rect["t"][0] - 2 * rect["s"][1]  # the edge between the reaches
        for t in (0.25, 0.5, 0.75):  # the inner face lies on the edge, the box outside it
            on = (ax + dx * length * t, -depth / 2, az + dz * length * t)
            assert box.sdf(on) == pytest.approx(0.0, abs=1e-6)
            assert box.sdf((on[0] + nx * 0.01, on[1], on[2] + nz * 0.01)) < 0
            assert box.sdf((on[0] - nx * 0.01, on[1], on[2] - nz * 0.01)) > 0
    for shape in shapes[1:1 + len(fill) + len(edges)]:
        assert shape["material"] == "straw" and shape["center"][1] == pytest.approx(-depth / 2)
        assert shape["size"][1] == pytest.approx(depth)
    assert [s["shape"] for s in shapes[-3:]] == ["sphere"] * 3
    assert all(s["landable"] for s in shapes)


def test_mounds_keep_clear_of_the_bare_patches(built, bare):
    for m in [c for c in built["collision"] if c["shape"] == "sphere"]:
        for patch in bare:
            x0, x1, z0, z1 = patch["window"]
            x, z = m["center"][0], m["center"][2]
            assert math.hypot(max(x0 - x, 0, x - x1), max(z0 - z, 0, z - z1)) >= ground_patch.MOUND_GAP_M


def test_baked_straws_over_a_floor_lie_on_it(built, s04_params, bare):
    """N8: the bake rests on the stepped surface, so the straws inside an outline lie on its floor (their median
    height near -D), where the mat's straws lie near 0."""
    depth = s04_params["mat_depth_m"]
    mesh = built["micro"][0]["mesh"]
    on_floor, on_mat = [], []
    for x, y, z in mesh.vertices:
        if math.hypot(x, z) > s04_params["bake_radius_m"] - s04_params["bake_feather_m"]:
            continue
        d = min(patches.distance((x, z), p["outline"]) for p in bare)
        (on_floor if d < -0.1 else on_mat if d > 0.6 else []).append(y)
    print(f"median straw vertex: {statistics.median(on_floor):.3f} m over floors ({len(on_floor)}), "
          f"{statistics.median(on_mat):.3f} m over the mat ({len(on_mat)})")
    assert len(on_floor) > 1000
    assert abs(statistics.median(on_floor) + depth) < 0.04 and abs(statistics.median(on_mat)) < 0.04


def test_mound_spheres_sit_within_5cm_of_their_raised_cells(built, s04_params):
    mounds = [c for c in built["collision"] if c["shape"] == "sphere"]
    assert len(mounds) == round(s04_params["mounds_per_m2"] * s04_params["size_m"] ** 2) == 3
    lod0 = built["lods"][0]
    for m in mounds:
        assert m["material"] == "surface:crater_spoil" and m["landable"]
        cx, cy, cz, r = m["center"][0], m["center"][1], m["center"][2], m["radius"]
        assert cy - r >= -slab_m(s04_params) - 1e-9, "the sphere stays inside the slab"
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
    assert hi[1] >= s04_params["stem_h_m"]["max"] * 0.9 and lo[1] >= -slab_m(s04_params), "buried ends stay inside the slab"


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
        ("ground", "surface:belt_straw"), ("soil", "surface:crater_spoil"), ("straw", "straw")]


@pytest.mark.parametrize("edit, expected", [
    (lambda p: p.update(bake_radius_m=7.0), "'bake_radius_m' = 7.0 reaches beyond the patch; at most size_m / 2 = 6"),
    (lambda p: p.update(bake_feather_m=4.5), "'bake_feather_m' = 4.5 exceeds 'bake_radius_m' = 4.0"),
    (lambda p: p["mound_h_m"].update(min=0.03), "'mound_h_m.min' = 0.03 must rise above 'relief_m' = 0.04"),
    (lambda p: p.update(cell_m=0.3), "'cell_m' = 0.3 cannot follow the mound spheres"),
    (lambda p: p["mound_w_m"].update(min=0.6), "'mound_w_m.min' = 0.6 exceeds 'mound_w_m.max' = 0.5"),
    (lambda p: p["lod"].update(lod2_switch_m=5.0), "'lod.lod1_switch_m' < 'lod.lod2_switch_m' < 'lod.cull_m' must hold"),
    (lambda p: p["bare"]["size_m"].update(min=2.5), "'bare.size_m.min' = 2.5 exceeds 'bare.size_m.max' = 2.0"),
    (lambda p: p["bare"]["size_m"].update(min=0.35), "'bare.size_m.min' = 0.35 must exceed the cell's diagonal 0.354 m"),
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
