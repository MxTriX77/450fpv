"""The vendor pieces' physics (w4b-farmland-ruins): their params against the generator, the committed metadata
against schema 1 and the physics table, and, where the local store is linked, a rebuild byte for byte."""
import json
import os

import pytest

from generators import vendor_ruin
from lib import catalog, meta, params as params_lib

import vendor_build

MODELS = ("unity", "Assets", "_Project", "Art", "Models", "ruins")


def variants(repo_root):
    assets = vendor_build.load(str(repo_root))
    return assets, [(a, v) for a in sorted(assets) for v in assets[a]["variants"]]


def load(repo_root, asset_id, variant):
    path = repo_root.joinpath(*MODELS, asset_id, variant + ".assetmeta.json")
    return json.loads(path.read_text(encoding="utf-8")), path


def test_every_variant_has_params_the_generator_accepts(repo_root):
    assets, pairs = variants(repo_root)
    assert len(pairs) == 13
    for asset_id, variant in pairs:
        params = catalog.load_params(catalog.params_path(str(repo_root), assets[asset_id], variant))
        assert params_lib.validate(vendor_ruin, params) == [], variant


def test_committed_metadata_validates_and_names_every_shapes_part(repo_root, material_ids):
    _, pairs = variants(repo_root)
    for asset_id, variant in pairs:
        data, path = load(repo_root, asset_id, variant)
        assert meta.validate(data, material_ids) == [], variant
        assert data["vendor"]["object"] in variant
        assert data["collision"] and all(c.get("part") for c in data["collision"]), variant
        assert {c["material"] for c in data["collision"]} <= {"timber", "masonry", "asbestos_cement"}, variant
        ids = [g["id"] for g in data["gaps"]]
        assert len(ids) == len(set(ids)), f"{variant}: gap ids are unique"
        assert os.path.exists(str(path) + ".meta"), "Unity's .meta beside the metadata"


def test_the_ruins_keep_their_walls_openings_and_floor(repo_root):
    data, _ = load(repo_root, "N01", "Ruin_RoofCollapsed")
    parts = {c["part"] for c in data["collision"]}
    assert {"wall_back", "wall_front", "wall_stove_end", "wall_far_end", "partition", "floor", "roof", "ceiling",
            "window_frame", "chimney", "stove"} <= parts
    assert all(c["landable"] == (c["part"] == "floor") for c in data["collision"])
    assert any(g["id"].startswith("wall_back_window") for g in data["gaps"])
    assert any(g["id"].startswith("roof_hole") for g in data["gaps"]), "the collapse holes the roof"
    assert any(w.get("cavity") for w in data["wind_volumes"])
    walls, _ = load(repo_root, "N01", "Ruin_Walls")
    assert not any(c["part"] in ("roof", "ceiling") for c in walls["collision"]), "the roofless ruin keeps no roof"


def test_a_broken_wall_is_its_two_skins_open_between_them(repo_root):
    # the back wall of the collapsed-roof ruin at x -3.0, 1.5 m up (the sim test's crash point): the siding's face at
    # z -4.552 and the plaster's at -4.352, the outside reaching the space between them through the broken tops
    data, _ = load(repo_root, "N01", "Ruin_RoofCollapsed")

    def parts_at(z):
        out = []
        for c in data["collision"]:
            lo = [c["center"][k] - c["size"][k] / 2 for k in range(3)]
            hi = [c["center"][k] + c["size"][k] / 2 for k in range(3)]
            if c["rotation"] == [0.0, 0.0, 0.0, 1.0] and all(lo[k] <= p <= hi[k] for k, p in enumerate((-3.0, 1.51, z))):
                out.append((c["part"], c["material"]))
        return out

    assert parts_at(-4.545) == [("wall_back", "timber")], "the siding"
    assert parts_at(-4.36) == [("wall_back", "masonry")], "the plaster"
    for z in (-4.52, -4.45, -4.38):
        assert parts_at(z) == [], f"open between the skins at z {z}"


def test_the_mirrored_ruin_is_the_ruin_mirrored(repo_root):
    a, _ = load(repo_root, "N01", "Ruin_Walls")
    b, _ = load(repo_root, "N01", "Ruin_Walls_mirrored")
    assert b["bounds"]["min"][0] == pytest.approx(-a["bounds"]["max"][0])
    assert b["bounds"]["max"][0] == pytest.approx(-a["bounds"]["min"][0])
    assert len(b["collision"]) == pytest.approx(len(a["collision"]), rel=0.05)
    ga, gb = {g["id"]: g for g in a["gaps"]}, {g["id"]: g for g in b["gaps"]}
    for gid in set(ga) & set(gb):  # one id, one opening: numbered in the source mesh's frame
        ca = [sum(p[k] for p in ga[gid]["polygon"]) / 4 for k in range(3)]
        cb = [sum(p[k] for p in gb[gid]["polygon"]) / 4 for k in range(3)]
        assert (cb[0], cb[1], cb[2]) == pytest.approx((-ca[0], ca[1], ca[2]), abs=0.03), gid
    for gid in set(ga) ^ set(gb):  # a pane cut to within a few cm of the smallest gap may fall either side of it
        g = ga.get(gid) or gb.get(gid)
        assert g["narrowest_m"] < 0.25 + 0.05, f"{gid} is in one of the two only"
    assert len(set(ga) & set(gb)) >= 0.8 * max(len(ga), len(gb))


def test_unity_euler_turns_x_onto_y_about_z():
    q = vendor_ruin.unity_euler([0.0, 0.0, 90.0])
    assert vendor_ruin._rotate(q, (1.0, 0.0, 0.0)) == pytest.approx((0.0, 1.0, 0.0), abs=1e-12)


def test_classify_takes_the_first_matching_rule():
    rules = [{"part": "floor", "physics": "timber", "normal": "up", "box": [[-1, -1, -1], [1, 0.6, 1]]},
             {"part": "roof", "physics": "asbestos_cement", "slots": ["Slate"], "normal": "sloped"},
             {"part": "trim", "physics": "timber"}]
    assert vendor_ruin.classify(rules, "Wood", (0, 1, 0), (0, 0.4, 0)) == 0
    assert vendor_ruin.classify(rules, "Slate_Burnt", (0, 0.8, 0.6), (0, 5, 0)) == 1, "a burnt copy is its slot"
    assert vendor_ruin.classify(rules, "Wood", (0, 0.8, 0.6), (0, 5, 0)) == 2


@pytest.mark.parametrize("name", ["N02", "Ruin_Stump"])
def test_a_rebuild_reproduces_the_committed_files(repo_root, name, capsys):
    if not os.path.exists(os.path.join(str(repo_root), "vendor", "world_final")):
        pytest.skip("the local store is not linked here (pwsh tools/vendor/link.ps1)")
    assert vendor_build.main([name, "--check"]) == 0
    assert "DIFFERS" not in capsys.readouterr().out
