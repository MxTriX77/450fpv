"""lib.straw, the S04 instance kit (p1-s04-belt-straw, design decision 4; task 1.7): every kind's meshes come in
`variants` distinct seeded copies within the sheet's ranges, a straw is a three-sided tube of `segments` segments
(24 triangles at 4), a headed stem adds the four-triangle seed head, a blade is a two-segment tube, normals point
outward, every face is UV'd into its palette column (straws and stems cycling through the shades by variant, twigs
to "twig", blades to "blade") with its v rising from the base to the tip, place() shifts an instance's v within the
map, and the palette's column order is the straw recipe's colour order, padded to a power of two."""
import json
import math
import random

import pytest

from lib import straw
from lib.geometry import Mesh

KIT = {"straw_len_m": {"min": 0.3, "max": 1.0}, "straw_d_mm": {"min": 2.0, "max": 5.0}, "straw_bend": 0.3,
       "stem_h_m": {"min": 0.5, "max": 1.2}, "seed_head_share": 0.45,
       "twig_len_m": {"min": 0.3, "max": 1.0}, "twig_d_mm": {"min": 5.0, "max": 20.0}, "variants": 8, "segments": 4,
       "blade_h_m": {"min": 0.05, "max": 0.15}}


def kit(seed=1):
    return straw.build_kit(KIT, random.Random(seed))


def test_every_kind_has_distinct_seeded_variants():
    a, b, again = kit(1), kit(2), kit(1)
    for kind in straw.KINDS:
        assert len(a[kind]) == KIT["variants"]
        assert len({tuple(m.vertices) for m in a[kind]}) == KIT["variants"], f"{kind}: variants repeat"
        assert [m.vertices for m in a[kind]] == [m.vertices for m in again[kind]], f"{kind}: not reproducible"
        assert [m.vertices for m in a[kind]] != [m.vertices for m in b[kind]], f"{kind}: the seed does nothing"


def test_tube_counts_and_sizes_follow_the_table():
    meshes = kit()
    for kind, length, diameter in (("straw", KIT["straw_len_m"], KIT["straw_d_mm"]), ("twig", KIT["twig_len_m"], KIT["twig_d_mm"])):
        for m in meshes[kind]:
            lo, hi = m.bounds()
            assert m.triangles() == 2 * straw.SIDES * KIT["segments"] == 24
            assert len(m.vertices) == straw.SIDES * (KIT["segments"] + 1)
            assert length["min"] * 0.9 <= hi[0] - lo[0] <= length["max"] * 1.01, f"{kind} length {hi[0] - lo[0]}"
            assert abs(lo[0] + hi[0]) < 0.3 * (hi[0] - lo[0]), f"{kind} is not centred on the origin"
            ring = m.vertices[:straw.SIDES]  # the base ring, around the spine's first point
            centre = [sum(v[k] for v in ring) / straw.SIDES for k in range(3)]
            base = max(math.dist(v, centre) for v in ring)
            assert diameter["min"] / 2000 - 1e-9 <= base <= diameter["max"] / 2000 + 1e-9
    for m in meshes["stem"]:
        lo, hi = m.bounds()
        assert m.triangles() == 24 and KIT["stem_h_m"]["min"] <= hi[1] <= KIT["stem_h_m"]["max"] * 1.01
        assert lo[1] == pytest.approx(0.0, abs=0.003), "a stem stands on its foot"
    for m in meshes["stem_head"]:
        assert m.triangles() == 28 and len(m.vertices) == 15 + 4
        assert m.bounds()[1][1] >= KIT["stem_h_m"]["min"] + straw.HEAD_H_M * 0.9
    for m in meshes["blade"]:
        lo, hi = m.bounds()
        assert m.triangles() == 2 * straw.SIDES * straw.BLADE_SEGMENTS == 12
        assert KIT["blade_h_m"]["min"] * 0.9 <= hi[1] <= KIT["blade_h_m"]["max"] * 1.01
        assert lo[1] == pytest.approx(0.0, abs=0.002), "a blade stands on its foot"


def test_normals_point_outward_and_faces_take_their_palette_column():
    """Each face in its column; along a tube a corner's v rises with its distance from the base ring, from
    V_ALONG[0] to V_ALONG[1] (a darker base, a paler tip)."""
    meshes = kit()
    for kind in straw.KINDS:
        for n, m in enumerate(meshes[kind]):
            colour = kind if kind in ("twig", "blade") else straw.SHADES[n % len(straw.SHADES)]
            base = m.vertices[0]
            pairs = []
            for indices, normal, uvs, slot in m.faces:
                assert slot == straw.SLOT and abs(math.sqrt(sum(c * c for c in normal)) - 1) < 1e-9
                assert all(u == straw.palette_uv(colour)[0] for u, _ in uvs), f"{kind} {n} leaves its column"
                assert all(straw.V_ALONG[0] - 1e-9 <= v <= straw.V_ALONG[1] + 1e-9 for _, v in uvs)
                pairs += [(math.dist(m.vertices[i], base), v) for i, (_, v) in zip(indices, uvs)]
            pairs.sort()
            assert pairs[0][1] == pytest.approx(straw.V_ALONG[0]) and pairs[-1][1] == pytest.approx(straw.V_ALONG[1])
    # Outward, on a straight tube whose spine is known: every face normal points away from the spine.
    tube = straw.tube([(0.0, 0.0, 0.0), (0.1, 0.0, 0.0), (0.2, 0.0, 0.0)], 0.002, 0.4, (0.0, 0.0, 1.0), "straw")
    assert tube.triangles() == 12
    for indices, normal, _, _ in tube.faces:
        centroid = [sum(tube.vertices[i][k] for i in indices) / len(indices) for k in range(3)]
        assert centroid[1] * normal[1] + centroid[2] * normal[2] > 0
    straw.head(tube, (0.2, 0.0, 0.0), (1.0, 0.0, 0.0), 0.0, "straw")
    assert tube.triangles() == 16 and tube.bounds()[1][0] == pytest.approx(0.2 + straw.HEAD_H_M)
    for indices, normal, _, _ in tube.faces[12:]:
        centroid = [sum(tube.vertices[i][k] for i in indices) / len(indices) for k in range(3)]
        assert sum((centroid[k] - (0.2 + straw.HEAD_H_M / 3, 0.0, 0.0)[k]) * normal[k] for k in range(3)) > 0


def test_palette_columns_are_the_straw_recipe_colours_in_order(repo_root):
    """The recipe's colours in PALETTE's order, each a gradient of three stops, the map padded to a power of two of
    columns as derive.py writes it (its stamp records the columns and the map's width)."""
    recipe = json.loads((repo_root / "assets-src" / "materials" / "straw.json").read_text(encoding="utf-8"))
    assert recipe["kind"] == "palette" and tuple(recipe["colours"]) == straw.PALETTE
    assert all(isinstance(v, list) and len(v) == 3 for v in recipe["colours"].values())
    assert set(straw.SHADES) < set(straw.PALETTE) and straw.shade(0) != straw.shade(1) != straw.shade(2)
    assert straw.COLUMNS == 8 and straw.COLUMNS >= len(straw.PALETTE) > straw.COLUMNS // 2
    assert straw.palette_uv("straw_pale") == (0.0625, 0.5)
    assert straw.palette_uv("blade", 1.0) == (0.5625, straw.V_LIMITS[1])
    stamp = repo_root / "unity" / "Assets" / "_Project" / "Art" / "Materials" / "straw.recipe.json"
    if stamp.is_file():
        built = json.loads(stamp.read_text(encoding="utf-8"))
        assert built["columns"] == list(straw.PALETTE)
        assert built["size"][0] == straw.COLUMNS * built["constants"]["PALETTE_BLOCK_PX"]


def test_straw_shades_average_to_the_sheets_dry_straw(repo_root):
    """The shades a straw or stem takes, each column averaged over its gradient (three stops spread evenly and
    interpolated in linear light: (s0 + 2 s1 + s2) / 4) and the columns averaged, land within 3 % luminance of the
    sheet's dry straw #b49c6a, so the geometry straws stay at the sheet's value while varying as H's do; every
    column runs dark to pale."""
    def linear(hex_colour):
        return [((c / 255 + 0.055) / 1.055) ** 2.4 if c / 255 > 0.04045 else c / 255 / 12.92
                for c in (int(hex_colour[i:i + 2], 16) for i in (1, 3, 5))]

    def luminance(rgb):
        return 0.2126 * rgb[0] + 0.7152 * rgb[1] + 0.0722 * rgb[2]

    colours = json.loads((repo_root / "assets-src" / "materials" / "straw.json").read_text(encoding="utf-8"))["colours"]
    shades = []
    for name in straw.SHADES:
        s0, s1, s2 = (linear(c) for c in colours[name])
        assert luminance(s0) < luminance(s1) < luminance(s2), f"{name} runs dark to pale"
        shades.append([(a + 2 * b + c) / 4 for a, b, c in zip(s0, s1, s2)])
    mean = [sum(c[k] for c in shades) / len(shades) for k in range(3)]
    assert abs(luminance(mean) / luminance(linear("#b49c6a")) - 1) <= 0.03


def test_place_rotates_and_moves_an_instance():
    variant = kit()["straw"][0]
    target = Mesh()
    straw.place(target, variant, (1.0, 2.0, 3.0), straw.lying_axes(math.pi / 2, 0.0, 0.0))
    assert len(target.vertices) == len(variant.vertices) and len(target.faces) == len(variant.faces)
    assert [f[2] for f in target.faces] == [f[2] for f in variant.faces], "no shade: the kit's UVs"
    darker = Mesh()
    straw.place(darker, variant, (0.0, 0.0, 0.0), straw.lying_axes(0.0, 0.0, 0.0), -0.4)
    for (_, _, uvs, _), (_, _, shifted, _) in zip(variant.faces, darker.faces):
        for (u, v), (su, sv) in zip(uvs, shifted):
            assert su == u and sv == pytest.approx(max(straw.V_LIMITS[0], v - 0.4))
    for (x, y, z), (px, py, pz) in zip(variant.vertices, target.vertices):
        assert (px, py, pz) == pytest.approx((1.0 - z, 2.0 + y, 3.0 + x))  # +X turned to +Z
    ex, ey, ez = straw.standing_axes(0.3, 0.1)
    for a, b in ((ex, ey), (ey, ez), (ez, ex)):
        assert sum(p * q for p, q in zip(a, b)) == pytest.approx(0.0, abs=1e-12)
    assert ey[1] == pytest.approx(math.cos(0.1))
