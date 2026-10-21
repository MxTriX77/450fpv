"""generators.debris_car: the four K07 pieces R01 needs (p1-r01-vaz-sedan, design decision 9): their shapes within
5 mm of the params, their primitives, materials and snags, the unused-size refusals, determinism per seed, and each
piece's audit against its LOD0 mesh (the spec's "Pieces built and composed")."""
import copy
import json

import pytest

from generators import debris_car
from lib import audit, catalog, meta, params as params_lib

MM5 = 0.005


@pytest.fixture(scope="module")
def k07(repo_root_module):
    entry = catalog.load(repo_root_module)["assets"]["K07"]
    return {v: catalog.load_params(catalog.params_path(repo_root_module, entry, v)) for v in entry["variants"]}


@pytest.fixture(scope="module")
def repo_root_module():
    from pathlib import Path
    return Path(__file__).resolve().parents[3]


def size_of(built):
    lo, hi = built["lods"][0].bounds()
    return [hi[a] - lo[a] for a in range(3)]


def test_catalog_lists_the_four_pieces(k07):
    assert sorted(k07) == ["K07_bead_wire", "K07_bezel_ring", "K07_lamp_bowl", "K07_trim_plate"]
    for variant, params in k07.items():
        assert params_lib.validate(debris_car, params) == [], variant
        assert params["piece"] == variant[len("K07_"):] and params["physics_material"] == "steel"


def test_shapes_within_5_mm(k07):
    bowl = size_of(debris_car.build(k07["K07_lamp_bowl"]))
    assert bowl == pytest.approx([0.17, 0.085, 0.17], abs=MM5)
    ring = size_of(debris_car.build(k07["K07_bezel_ring"]))
    assert ring == pytest.approx([0.18, 0.012, 0.18], abs=MM5)
    plate = size_of(debris_car.build(k07["K07_trim_plate"]))
    assert plate[0] == pytest.approx(0.35, abs=MM5) and plate[2] == pytest.approx(0.2, abs=MM5)
    assert plate[1] == pytest.approx(0.001 + 0.01, abs=MM5)
    built = debris_car.build(k07["K07_bead_wire"])
    wire, bend = size_of(built), k07["K07_bead_wire"]["bend_m"]
    centre = built["snag"][0]["points"][:-1]
    mean_r = sum((p[0] ** 2 + p[2] ** 2) ** 0.5 for p in centre) / len(centre)
    assert 2 * mean_r + 0.006 == pytest.approx(0.37, abs=MM5)  # its mean outer diameter
    assert all(abs(wire[a] - 0.37) <= 2 * bend + 1e-9 for a in (0, 2)) and wire[1] == pytest.approx(0.006, abs=1e-6)


def test_primitives_materials_and_snags(k07):
    bowl = debris_car.build(k07["K07_lamp_bowl"])
    assert [c["shape"] for c in bowl["collision"]] == ["sphere"]
    # the pressed rim: a closed edge on the ground round the bowl's 0.17 m, at half its 0.8 mm wall
    (rim,) = bowl["snag"]
    assert rim["kind"] == "edge" and rim["radius_m"] == pytest.approx(0.0004) and rim["points"][0] == rim["points"][-1]
    assert all(p[1] == 0.0 and (p[0] ** 2 + p[2] ** 2) ** 0.5 == pytest.approx(0.085 - 0.0004) for p in rim["points"])
    ring = debris_car.build(k07["K07_bezel_ring"])
    assert [c["shape"] for c in ring["collision"]] == ["capsule"] * debris_car.RING_CAPSULES
    assert [s["kind"] for s in ring["snag"]] == ["edge"] and ring["snag"][0]["radius_m"] == pytest.approx(0.006)
    plate = debris_car.build(k07["K07_trim_plate"])
    assert [c["shape"] for c in plate["collision"]] == ["box"]
    assert plate["snag"][0]["kind"] == "edge" and plate["snag"][0]["radius_m"] == pytest.approx(0.0005)
    wire = debris_car.build(k07["K07_bead_wire"])
    assert [s["kind"] for s in wire["snag"]] == ["hook"] and wire["snag"][0]["radius_m"] == pytest.approx(0.003)
    assert wire["materials"] == [{"slot": "rust", "physics": "steel"}]
    for built in (bowl, ring, plate, wire):
        assert all(c["material"] == "steel" and c["landable"] is False for c in built["collision"])
        assert built["thin_wire"] == [False, False] and built["gaps"] == [] and built["wind_volumes"] == []
        assert built["lods"][1].triangles() * 2 <= built["lods"][0].triangles()


def test_unused_sizes_are_refused(k07):
    params = copy.deepcopy(k07["K07_lamp_bowl"])
    params["length_m"] = 0.3
    assert params_lib.validate(debris_car, params) == ["'length_m' = 0.3 is not used by a lamp_bowl; set it to 0"]
    params = copy.deepcopy(k07["K07_trim_plate"])
    params["piece"] = "hubcap"
    assert params_lib.validate(debris_car, params) == [
        "'piece' = 'hubcap' must be one of lamp_bowl, bezel_ring, trim_plate, bead_wire"]


def test_seed_moves_only_the_seeded_shapes(k07):
    wire = k07["K07_bead_wire"]
    a = debris_car.build(wire)["lods"][0].vertices
    assert a == debris_car.build(wire)["lods"][0].vertices
    assert a != debris_car.build(dict(wire, seed=2))["lods"][0].vertices


@pytest.mark.parametrize("variant", ["K07_lamp_bowl", "K07_bezel_ring", "K07_trim_plate", "K07_bead_wire"])
def test_each_piece_passes_the_audit(k07, variant, repo_root_module, material_ids):
    built = debris_car.build(k07[variant])
    data = meta.assemble("K07", variant, "debris_car", k07[variant], built, {"chrome": "chrome", "rust": "car_rust"})
    assert meta.validate(data, material_ids) == []
    problems, _ = audit.run(data, built["lods"][0], soft=audit.soft_materials(repo_root_module), planted=True)
    assert problems == []
