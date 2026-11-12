"""The plants' numbers (w9-village-physics 2.8): the stem cut on a synthetic card whose answer is known, the committed
file against the map's grass layers and the plant catalog, and, with vendor/ linked, the grass section re-measured
from the store equal to the committed one (the crowns take minutes; `vendor_plant_numbers.py --check` re-runs all)."""
import json
import os

import pytest

import vendor_plant_numbers as numbers


class _Stripes:
    """Alpha of vertical stripes: opaque where int(u * 20) is even, so 10 stripes across u in [0, 1)."""
    data = True

    def opaque(self, u, v):
        return int(u * 20) % 2 == 0


def test_a_card_of_ten_stripes_cut_across_two_triangles_gives_ten_stems():
    # a 1 m x 1 m upright card in two triangles, u along x; the diagonal splits several stripes between them
    points = [(0.0, 0.0, 0.0), (1.0, 0.0, 0.0), (1.0, 1.0, 0.0), (0.0, 1.0, 0.0)]
    tris = [(0, 1, 2), (0, 2, 3)]
    uvs = [((0.0, 0.0), (1.0, 0.0), (1.0, 1.0)), ((0.0, 0.0), (1.0, 1.0), (0.0, 1.0))]
    widths = numbers.cut(points, tris, uvs, _Stripes(), (1000.0, 1000.0), 0.4321)
    assert len(widths) == 10
    assert all(abs(w - 0.05) < 0.002 for w in widths)


def test_two_unit_spheres_apart_and_overlapping():
    one = 4.0 / 3.0 * 3.141592653589793
    assert abs(numbers.shell_volume([((0, 0, 0), 1.0), ((5, 0, 0), 1.0)]) - 2 * one) < 0.01 * one
    same = numbers.shell_volume([((0, 0, 0), 1.0), ((0, 0, 0), 1.0)])
    assert abs(same - one) < 0.01 * one


def test_the_committed_numbers_cover_every_layer_and_variant(repo_root):
    data = json.loads((repo_root / numbers.OUT).read_text(encoding="utf-8"))
    layers = json.loads((repo_root / numbers.MAP).read_text(encoding="utf-8"))["grass"]["layers"]
    assert [(l["layer"], l["prefab"]) for l in data["grass"]["layers"]] == [(l["layer"], l["prefab"]) for l in layers]
    for row in data["grass"]["layers"]:
        assert row["stems_per_instance"] == data["grass"]["prefabs"][row["prefab"]]["stems"] > 0
    plants = json.loads((repo_root / numbers.PLANTS).read_text(encoding="utf-8"))
    variants = sorted(v for e in plants["assets"].values() for v in e["variants"])
    assert sorted(data["crowns"]) == variants
    for crown in data["crowns"].values():
        assert crown["leaf_area_m2"] >= 0 and crown["crown_volume_m3"] > 0


def test_the_grass_re_measures_to_the_committed_numbers(repo_root):
    if not os.path.isdir(os.path.join(repo_root, numbers.STORE)):
        pytest.skip("the local store is not linked (pwsh tools/vendor/link.ps1)")
    data = json.loads((repo_root / numbers.OUT).read_text(encoding="utf-8"))
    prefabs, layers = numbers.grass(str(repo_root), numbers.guid_index(str(repo_root)))
    assert json.loads(json.dumps({"prefabs": prefabs, "layers": layers})) == data["grass"]
