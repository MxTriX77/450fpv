"""generators.ground_patch with the S04 params (p1-s04-belt-straw, design decisions 1, 4, 6, 8; review-physics PR1-PR8;
task 1.7): the slab's top is the mean at y = 0 with the relief inside +-relief_m and is lib.mat's tussock relief,
the mounds' spheres sit within 5 cm of their raised cells, a mound's top is bare soil, the micro mesh holds the
kit's scatter at its densities over the feathered bake area within 5 % (the straws gathered on the crowns, few over
a floor, none of the stems and blades there, the blades by the green strips' weight), the feather thins the rim,
the metadata lists it, and `check` refuses a bake radius beyond the patch, a feather wider than the bake, and a
mound the relief or the grid cannot carry. The bare patches: the mesh steps down by the mat depth along each
outline to a floor whose walls are the mat's cut edge, no shape stands above a floor and the soil box lies just
under it (PR7 a), the old single mat box fails the audit by the step (PR7 b), the shapes come in PR8's order, the
mat boxes are the layout's, and the baked straws over a floor lie on it (N8). The loft: the lying straws stack
above the mat's top, more on the crowns, never above a 20 cm mat; straws overhang a step's lip; the blades stand
only over the green strips."""
import copy
import math
import statistics

import pytest

from generators import ground_patch
from lib import audit, mat, meta, params, patches, straw
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
    perimeter; the floor takes the ground slot, a wall the straw slot up the litter's palette column (the mat's
    cut edge, darker at its foot)."""
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
                assert slot == straw.SLOT
                assert {u for u, _ in uvs} == {straw.palette_uv("litter")[0]}
                for point, (_, v) in zip(points, uvs):
                    top = point[1] == max(p[1] for p in points if abs(p[0] - point[0]) + abs(p[2] - point[2]) < 1e-9)
                    assert v == pytest.approx(ground_patch.WALL_V[1] if top else ground_patch.WALL_V[0])
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
        near = [p for p in bare if p["window"][0] - 0.6 <= x <= p["window"][1] + 0.6
                and p["window"][2] - 0.6 <= z <= p["window"][3] + 0.6]
        d = min([patches.distance((x, z), p["outline"]) for p in near] or [1.0])
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


def expected_instances(p, step=0.05):
    """The instances the bake should hold, integrated every `step` over the feathered disc: the lying straws of
    both layers at (1 + clump c) of their density on the mat (c the relief over relief_m), thinned over the green
    strips by GREEN_THIN, and FLOOR_KEEP of it over a floor, the lip straws along the outlines, the stems likewise on the mat only, the blades by the green strips' weight on the mat, the twigs
    everywhere."""
    relief = mat.relief(**mat.relief_inputs(p))
    bare = patches.layout(**patches.inputs(p))["patches"]
    strips = mat.green(**mat.green_inputs(p), bare_patches=bare)["strips"]
    radius, feather, clump = p["bake_radius_m"], p["bake_feather_m"], p["clump"]
    lying = p["straw_per_m2"] + p["body_straw_per_m2"]
    n, total = int(math.ceil(radius / step)), 0.0
    for i in range(-n, n):
        for j in range(-n, n):
            x, z = (i + 0.5) * step, (j + 0.5) * step
            r = math.hypot(x, z)
            if r > radius:
                continue
            kept = 1.0 if r <= radius - feather else ground_patch._smooth((radius - r) / feather)
            d = ground_patch._floor_depth(bare, x, z)
            if d is None:
                gathered, green = 1 + clump * relief(x, z) / p["relief_m"], mat.green_weight(strips, x, z)
                here = ((lying * (1 - ground_patch.GREEN_THIN * green) + p["stem_per_m2"]) * gathered
                        + p["blade_per_m2"] * green)
            else:
                edge = 1 - ground_patch._smooth(min(1.0, d / patches.FEATHER_M))
                keep = ground_patch.FLOOR_KEEP
                here = lying * (keep[0] + (keep[1] - keep[0]) * edge)
            total += kept * (here + p["twig_per_m2"]) * step * step
    for patch in bare:  # the lip straws along every outline, kept by the bake's feather where they lie
        for a, b in zip(patch["outline"], patch["outline"][1:] + patch["outline"][:1]):
            for k in range(100):
                x, z = a[0] + (b[0] - a[0]) * (k + 0.5) / 100, a[1] + (b[1] - a[1]) * (k + 0.5) / 100
                r = math.hypot(x, z)
                kept = 0.0 if r > radius else 1.0 if r <= radius - feather else ground_patch._smooth((radius - r) / feather)
                total += kept * ground_patch.LIP_STRAWS_PER_M * math.dist(a, b) / 100
    return total


def test_micro_mesh_holds_the_scatter_at_density_times_area(built, s04_params):
    micro = built["micro"]
    assert len(micro) == 1 and micro[0]["lod"] == 0 and micro[0]["kind"] == "straw"
    expected = expected_instances(s04_params)
    print(f"{micro[0]['count']} instances for {expected:.0f} expected, {micro[0]['mesh'].triangles()} triangles")
    assert abs(micro[0]["count"] / expected - 1) <= 0.05, f"{micro[0]['count']} instances for {expected:.0f}"
    mesh = micro[0]["mesh"]
    assert mesh.triangles() + built["lods"][0].triangles() < 450_000, "LOD0 within the task's budget"
    assert mesh.slots() == ["straw"]
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
    (lambda p: (p.update(cell_m=0.25), p["bare"]["size_m"].update(min=0.35)),
     "'bare.size_m.min' = 0.35 must exceed the cell's diagonal 0.354 m"),
    (lambda p: p["green"]["width_m"].update(min=3.5), "'green.width_m.min' = 3.5 exceeds 'green.width_m.max' = 3.0"),
    (lambda p: p["blade_h_m"].update(min=0.2), "'blade_h_m.min' = 0.2 exceeds 'blade_h_m.max' = 0.15"),
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
    for indices, normal, _, slot in lod0.faces:
        centroid = [sum(lod0.vertices[i][k] for i in indices) / len(indices) for k in (0, 2)]
        under = [m for m in mounds if math.hypot(centroid[0] - m["center"][0], centroid[1] - m["center"][2]) < 0.25]
        if slot == ground_patch.SOIL_SLOT:
            soil.append(centroid)
            assert under, f"a soil face at {centroid} lies on no mound"
        elif slot == straw.SLOT:
            assert abs(normal[1]) < 1e-9, "only a step's wall takes the straw slot"
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
        p.update(stem_per_m2=0.0, twig_per_m2=0.0, body_straw_per_m2=0.0, blade_per_m2=0.0, bake_feather_m=feather,
                 straw_len_m={"min": 0.05, "max": 0.06})
        mesh = ground_patch.build(p)["micro"][0]["mesh"]
        radius = p["bake_radius_m"]
        r = [math.hypot(x, z) for x, _, z in mesh.vertices]
        assert max(r) <= radius + 0.04
        inner = sum(1 for v in r if v < 2.5) / (math.pi * 2.5 ** 2)
        rim = sum(1 for v in r if radius - 0.3 <= v < radius - 0.03) / (math.pi * ((radius - 0.03) ** 2 - (radius - 0.3) ** 2))
        return rim / inner

    assert density(1.0) < 0.15, "the feathered rim thins out"
    assert density(0.0) > 0.85, "without a feather the rim is as dense as the middle"


def test_the_slab_is_the_shared_tussock_relief(built, s04_params, bare):
    """Task 1.7: away from the mounds and the floors, the grid's vertices lie on lib.mat's relief, the field the ground
    recipe darkens its hollows by, so the map's hollows are the mesh's."""
    relief = mat.relief(**mat.relief_inputs(s04_params))
    mounds = [c for c in built["collision"] if c["shape"] == "sphere"]
    checked = 0
    for x, y, z in built["lods"][0].vertices:
        if y <= -slab_m(s04_params) + 1e-9 or any(math.hypot(x - m["center"][0], z - m["center"][2]) < 0.5 for m in mounds):
            continue
        if min(patches.distance((x, z), p["outline"]) for p in bare) < 1e-6:  # a floor, or a wall's foot
            continue
        assert y == pytest.approx(relief(x, z), abs=1e-9)
        checked += 1
    assert checked > 8000


def lying_straws(s04_params):
    """The lying straws' centres (x, y, z) of a build with only them (no stems, twigs or blades, which leaves the
    straws' draws as they are: the kit draws every kind and the straws are scattered first), each the mean of its
    24-triangle tube's 15 vertices."""
    p = copy.deepcopy(s04_params)
    p.update(stem_per_m2=0.0, twig_per_m2=0.0, blade_per_m2=0.0)
    mesh, n = ground_patch.build(p)["micro"][0]["mesh"], straw.SIDES * (p["segments"] + 1)
    centres = []
    for start in range(0, len(mesh.faces), 2 * straw.SIDES * p["segments"]):
        first = min(mesh.faces[start][0])
        vertices = mesh.vertices[first:first + n]
        centres.append(tuple(sum(v[k] for v in vertices) / n for k in range(3)))
    return centres


def test_lying_straws_loft_over_the_crowns_and_stay_in_a_20cm_mat(s04_params, bare):
    """The loft: the lying straws over the mat rest above the mat's top, up to about loft_m, more of them and higher
    on the relief's crowns than in its hollows (heaps), none above a MAT_MAX_M mat (a straw's centre may sit up to
    its bow, BOW x straw_bend x its length x 2/pi, above the line between its ends); the mounds aside, where straws
    lie on the earth."""
    relief = mat.relief(**mat.relief_inputs(s04_params))
    top = ground_patch.MAT_MAX_M - s04_params["mat_depth_m"]
    mounds = [c["center"] for c in ground_patch.build(s04_params)["collision"] if c["shape"] == "sphere"]
    bow = straw.BOW * s04_params["straw_bend"] * s04_params["straw_len_m"]["max"] * 2 / math.pi
    crowns, hollows = [], []
    for x, y, z in lying_straws(s04_params):
        if patches.inside(bare, x, z) or math.hypot(x, z) > s04_params["bake_radius_m"] - s04_params["bake_feather_m"]:
            continue
        if any(math.hypot(x - m[0], z - m[2]) < 0.8 for m in mounds):
            continue
        c = relief(x, z) / s04_params["relief_m"]
        assert y <= top + bow + 0.005, f"a straw at {(x, y, z)} above a {ground_patch.MAT_MAX_M} m mat"
        (crowns if c > 0.3 else hollows if c < -0.3 else []).append((y, y - relief(x, z)))
    heights = {name: statistics.median(y for y, _ in v) for name, v in (("crowns", crowns), ("hollows", hollows))}
    lifts = {name: [lift for _, lift in v] for name, v in (("crowns", crowns), ("hollows", hollows))}
    print(f"{len(crowns)} straws on crowns at {heights['crowns']:.3f} m, {len(hollows)} in hollows at "
          f"{heights['hollows']:.3f} m; lifted {statistics.median(lifts['crowns']):.3f} / "
          f"{statistics.median(lifts['hollows']):.3f} m above the mat")
    assert len(crowns) > len(hollows) > 100, "gathered on the crowns"
    assert heights["crowns"] > heights["hollows"], "heaped on the crowns"
    assert min(statistics.median(v) for v in lifts.values()) > 0.0, "lofted over the mat everywhere"
    assert max(lifts["crowns"] + lifts["hollows"]) > 0.6 * s04_params["loft_m"]


def test_straws_overhang_a_steps_lip(built, s04_params, bare):
    """A ragged lip: lying-straw vertices over a floor within LIP_OVERHANG_M of its outline, held near the mat's top
    (not dropped to the floor D below), on every patch inside the bake."""
    depth = s04_params["mat_depth_m"]
    mesh = built["micro"][0]["mesh"]
    over = {}
    for x, y, z in mesh.vertices:
        if math.hypot(x, z) > s04_params["bake_radius_m"] - s04_params["bake_feather_m"]:
            continue
        for k, patch in enumerate(bare):
            w = patch["window"]
            if not (w[0] <= x <= w[1] and w[2] <= z <= w[3]):
                continue
            d = patches.distance((x, z), patch["outline"])
            if -ground_patch.LIP_OVERHANG_M < d < -0.02 and y > -depth / 2:
                over[k] = over.get(k, 0) + 1
    inside = [k for k, p in enumerate(bare) if math.hypot(*p["centre"]) < s04_params["bake_radius_m"] - 1.5]
    print(f"overhanging vertices per patch inside the bake: {over}")
    assert inside and all(over.get(k, 0) > 20 for k in inside)


def test_blades_stand_only_over_the_green_strips(built, s04_params, bare):
    """The regrowth blades (faces in the blade column) stand where the strips' weight is above 0 and never on a floor,
    and the strips inside the bake carry them."""
    strips = mat.green(**mat.green_inputs(s04_params), bare_patches=bare)["strips"]
    mesh = built["micro"][0]["mesh"]
    column = straw.palette_uv("blade")[0]
    faces = [f for f in mesh.faces if f[2][0][0] == column]
    per = 2 * straw.SIDES * straw.BLADE_SEGMENTS
    assert len(faces) % per == 0 and len(faces) // per > 1000
    for start in range(0, len(faces), per):  # the blades are scattered last, each one's faces together
        x, _, z = mesh.vertices[min(i for f in faces[start:start + per] for i in f[0])]  # its foot
        assert any(mat.green_weight(strips, x + dx, z + dz) > 0 for dx in (-0.03, 0, 0.03) for dz in (-0.03, 0, 0.03))
        assert min(patches.distance((x, z), p["outline"]) for p in bare) > -0.05, f"a blade on a floor at {(x, z)}"
