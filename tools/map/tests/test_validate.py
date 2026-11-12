"""The map package validator (tools/map/validate.py, w2-farmland-export 1.1): the triangulation it reads the heights
with, a small package built here that passes, each rule broken one at a time, and the committed maps/farmland."""
import copy
import hashlib
import json
import sys
from array import array
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import validate as v  # noqa: E402

FARMLAND = v.REPO / "maps" / "farmland"


# ------------------------------------------------------------------ the triangulation

def one_cell(h00, h10, h01, h11, size=(1.0, v.MAX_SAMPLE, 1.0)):
    """A 2 x 2-sample tile on (0, 0): h10 is the south-east corner, h01 the north-west (Unity's indices)."""
    samples = array("H", [h01, h11, h00, h10])  # rows north to south
    row = {"id": "t", "ring": "map", "position_m": [0, 0, 0], "size_m": list(size), "heights": {"resolution": 2}}
    return v.Tile(row, samples)


def test_a_cell_splits_on_its_south_west_to_north_east_diagonal():
    tile = one_cell(0, 100, 0, 0)  # only the south-east corner raised
    assert tile.height(0.75, 0.25) == pytest.approx(50.0)  # south-east triangle: 0 + 100·0.75 + (0 − 100)·0.25
    assert tile.height(0.25, 0.75) == pytest.approx(0.0)  # north-west triangle never sees the south-east corner
    assert tile.height(0.5, 0.5) == pytest.approx(0.0)  # on the diagonal both agree
    tile = one_cell(0, 0, 100, 0)  # only the north-west corner raised
    assert tile.height(0.25, 0.75) == pytest.approx(50.0)
    assert tile.height(0.75, 0.25) == pytest.approx(0.0)


def test_heights_are_samples_over_32766_of_the_tile_height_plus_its_y():
    row = {"id": "t", "ring": "map", "position_m": [0, 5.0, 0], "size_m": [1.0, 120.0, 1.0], "heights": {"resolution": 2}}
    tile = v.Tile(row, array("H", [v.MAX_SAMPLE] * 4))
    assert tile.height(0.3, 0.6) == pytest.approx(125.0)


def test_the_highest_overlapping_tile_is_the_ground():
    low = one_cell(0, 0, 0, 0)
    high = one_cell(10, 10, 10, 10)
    assert v.ground_height([low, high], 0.5, 0.5) == pytest.approx(10.0)
    assert v.ground_height([low], 2.0, 2.0) is None


# ------------------------------------------------------------------ a small package

def write_package(root, edit=None):
    """A valid 2-tile package (the map and a surround tile east of it) under root/<id>; edit(parts) may break it."""
    res, side, top = 3, 10.0, 100.0
    west = [[1000, 2000, 3000], [1500, 2500, 3500], [1200, 2200, 3200]]  # rows north to south
    east = [[3000, 3100, 3200], [3500, 3600, 3700], [3200, 3300, 3400]]  # its west column is the map's east column
    parts = {
        "heights/map.r16": array("H", sum(west, [])).tobytes(),
        "heights/surround_E.r16": array("H", sum(east, [])).tobytes(),
        "classes/map.raw": bytes([1, 6, 6, 13]),
        "classes/surround_E.raw": bytes([1, 1, 2, 1]),
        "grass/map.raw": bytes([2, 0, 1, 3]),
        "holes/map.raw": bytes([0, 255, 0, 0]),
    }
    tiles = [
        {"id": "map", "ring": "map", "position_m": [0, 0, 0], "size_m": [side, top, side],
         "heights": {"file": "heights/map.r16", "resolution": res, "min_sample": 1000, "max_sample": 3500},
         "classes": {"file": "classes/map.raw", "resolution": 2},
         "holes": {"file": "holes/map.raw", "resolution": 2}},
        {"id": "surround_E", "ring": "surround", "position_m": [side, 0, 0], "size_m": [side, top, side],
         "heights": {"file": "heights/surround_E.r16", "resolution": res, "min_sample": 3000, "max_sample": 3700},
         "classes": {"file": "classes/surround_E.raw", "resolution": 2}},
    ]
    manifest = {
        "format": v.FORMAT, "format_version": 1, "map": "tiny", "frame": v.FRAME,
        "source": {"scene": "Tiny", "generator": "test", "exporter": "test", "seeds": {"Seed": 1}},
        "map_rect_m": [0, 0, side, side],
        "tiles": tiles,
        "ground_classes": {"surface_table": "assets-src/surfaces.json", "layers": [
            {"layer": "Grass", "surface": "meadow_sod", "id": 1, "note": "test"},
            {"layer": "Mud", "surface": "dry_crust", "id": 2, "note": "test"},
            {"layer": "Furrows", "surface": "tilled", "id": 6, "note": "test"},
            {"layer": "Track", "surface": "dirt_track", "id": 13, "note": "test"}]},
        "grass": {"tile": "map", "file": "grass/map.raw", "resolution": 2,
                  "layers": [{"layer": 0, "prefab": "Grass_A", "group": "Green", "instances": 6}]},
        "counts": {"tree": 1, "bush": 1, "ruin": 1, "debris": 1, "ring_trees_left_out": 0},
        "height_probes": {"seed": 1},
    }
    header = {"format_version": 1, "map": "tiny", "frame": v.FRAME}
    placements = dict(header, format="fpv450-map-placements", variants=[
        {"id": "Bush", "kind": "bush"}, {"id": "Door", "kind": "debris"}, {"id": "Ruin_A", "kind": "ruin"}, {"id": "Tree", "kind": "tree"}],
        placements=[
            {"id": 0, "kind": "tree", "variant": "Tree", "position_m": [1, 3, 1], "rotation_xyzw": [0, 0.6, 0, 0.8], "scale": [1, 1, 1]},
            {"id": 1, "kind": "bush", "variant": "Bush", "position_m": [9, 3, 9], "rotation_xyzw": [0, 0, 0, 1], "scale": [1.2, 1, 1.2]},
            {"id": 2, "kind": "ruin", "variant": "Ruin_A", "position_m": [5, 3, 5], "rotation_xyzw": [0, 1, 0, 0], "scale": [-1, 1, 1]},
            {"id": 3, "kind": "debris", "variant": "Door", "position_m": [6, 3, 5], "rotation_xyzw": [0.6, 0, 0, 0.8], "scale": [1, 1, 1]}])
    outlines = dict(header, format="fpv450-map-outlines",
                    fields=[{"name": "F1", "overgrowth": 0.3, "furrow_axis": "X", "outline_m": [[1, 1], [4, 1], [4, 4], [1, 4]]}],
                    village={"shape": "circle", "centre_m": [5, 5], "radius_m": 2})
    probe_tiles = [v.Tile(t, array("H", parts[t["heights"]["file"]])) for t in tiles]
    points = [("map", 2.5, 7.25), ("map", 7.5, 2.75), ("map", 10.0, 5.0), ("surround", 13.0, 6.0)]
    probes = dict(header, format="fpv450-map-height-probes", about="test",
                  probes=[{"ring": r, "x": x, "z": z, "y": round(v.ground_height(probe_tiles, x, z), 6)} for r, x, z in points])
    parts["placements.json"] = json.dumps(placements).encode()
    parts["outlines.json"] = json.dumps(outlines).encode()
    parts["height_probes.json"] = json.dumps(probes).encode()
    parts["map.json"] = manifest
    if edit:
        edit(parts)
    manifest = parts.pop("map.json")
    if "files" not in manifest:
        manifest["files"] = {k: hashlib.sha256(b).hexdigest() for k, b in sorted(parts.items())}
    package = root / manifest["map"]
    for relative, data in parts.items():
        (package / relative).parent.mkdir(parents=True, exist_ok=True)
        (package / relative).write_bytes(data)
    (package / "map.json").write_text(json.dumps(manifest), encoding="utf-8")
    return package


def errors_of(tmp_path, edit=None):
    errors, _ = v.validate(write_package(tmp_path, edit))
    return errors


def jsonpart(parts, name, change):
    document = json.loads(parts[name])
    change(document)
    parts[name] = json.dumps(document).encode()


def test_a_valid_package_passes(tmp_path):
    errors, summary = v.validate(write_package(tmp_path))
    assert errors == []
    assert "2 tiles (1 seams)" in summary and "4 height probes" in summary
    assert v.main([str(tmp_path / "tiny")]) == 0


@pytest.mark.parametrize("edit, message", [
    (lambda p: p["map.json"].update(format_version=2), "format must be fpv450-map-package version 1"),
    (lambda p: p["map.json"].update(frame="godot"), "frame must be unity-lh-yup"),
    (lambda p: p["map.json"]["source"].update(latitude=None), "georeferencing field 'latitude'"),
    (lambda p: jsonpart(p, "outlines.json", lambda d: d["village"].update(EPSG=4326)), "georeferencing field 'EPSG'"),
    (lambda p: p.update({"classes/map.raw": b"version https://git-lfs.github.com/spec/v1\n"}), "a Git LFS pointer"),
    (lambda p: p["map.json"].update(files={"heights/map.r16": "0" * 64}), "SHA-256 differs"),
    (lambda p: p["map.json"]["tiles"][0]["heights"].update(resolution=4), "not 2^n + 1"),
    (lambda p: p.update({"heights/map.r16": p["heights/map.r16"][:-2]}), "height bytes"),
    (lambda p: p.update({"heights/map.r16": array("H", [40000] * 9).tobytes()}), "a sample above 32766"),
    (lambda p: p["map.json"]["tiles"][1].update(ring="moon"), "ring must be one of"),
    (lambda p: p["map.json"].update(map_rect_m=[0, 0, 12, 10]), "map_rect_m must be the map tile's rectangle"),
    (lambda p: p.update({"classes/map.raw": bytes([1, 6, 6, 9])}), "class ids [9] are not in the ground class table"),
    (lambda p: p["map.json"]["ground_classes"]["layers"][0].update(id=3), "is not surface id 3"),
    (lambda p: p["map.json"]["grass"].update(resolution=3), "grass: 4 bytes for 1 layers of 3 x 3"),
    (lambda p: p["map.json"]["grass"]["layers"][0].update(instances=7), "instance count differs"),
    (lambda p: jsonpart(p, "placements.json", lambda d: d["placements"][0]["position_m"].__setitem__(0, 10.5)), "lies outside the map"),
    (lambda p: jsonpart(p, "placements.json", lambda d: d["placements"][1].update(variant="Tree")), "is not a listed variant of that kind"),
    (lambda p: jsonpart(p, "placements.json", lambda d: d["placements"][2].update(rotation_xyzw=[0, 1, 0, 1])), "not a unit quaternion"),
    (lambda p: jsonpart(p, "placements.json", lambda d: d["placements"][3].update(id=7)), "ids must run 0, 1, 2"),
    (lambda p: p["map.json"]["counts"].update(tree=2), "1 tree rows, map.json counts 2"),
    (lambda p: jsonpart(p, "outlines.json", lambda d: d["fields"][0]["outline_m"].reverse()), "counter-clockwise"),
    (lambda p: jsonpart(p, "outlines.json", lambda d: d["village"].update(radius_m=0.5)), "a debris outside the village circle"),
    (lambda p: jsonpart(p, "height_probes.json", lambda d: d["probes"][1].update(y=d["probes"][1]["y"] + 0.0015)), "mm from Unity's"),
    (lambda p: jsonpart(p, "height_probes.json", lambda d: d["probes"][0].update(x=50.0)), "lies on no tile"),
    (lambda p: p.update({"holes/map.raw": bytes([0, 7, 0, 0])}), "a hole mask holds 0 (ground) or 255 (a hole) only"),
    (lambda p: p.update({"holes/map.raw": bytes([0, 255, 0])}), "3 hole bytes for 2 x 2 cells"),
    (lambda p: p["map.json"]["tiles"][0]["holes"].update(resolution=3), "a hole mask's resolution is the heightmap's less one"),
    (lambda p: (jsonpart(p, "placements.json", lambda d: (d["variants"].append({"id": "E01_curving", "kind": "trench"}),
                d["placements"].append({"id": 4, "kind": "trench", "variant": "E01_curving", "position_m": [2, 3, 2],
                                        "rotation_xyzw": [0, 0, 0, 1], "scale": [1, 1, 1]}))),
                p["map.json"]["counts"].update(trench=1)), "rows of ours, but map.json has no objects record"),
])
def test_each_rule_catches_its_break(tmp_path, edit, message):
    errors = errors_of(tmp_path, edit)
    assert any(message in e for e in errors), errors


def test_a_seam_step_between_abutting_tiles_is_reported(tmp_path):
    def step(parts):
        samples = array("H", parts["heights/surround_E.r16"])
        samples[3] += 1  # the surround's west column, middle row
        parts["heights/surround_E.r16"] = samples.tobytes()
        parts["map.json"]["tiles"][1]["heights"]["max_sample"] = max(samples)
    assert any("seam map | surround_E" in e for e in errors_of(tmp_path, step))


def test_an_unlisted_file_is_reported(tmp_path):
    package = write_package(tmp_path)
    (package / "stray.raw").write_bytes(b"\0")
    errors, _ = v.validate(package)
    assert any("stray.raw: in the package but not listed" in e for e in errors)


# ------------------------------------------------------------------ the committed package

def _farmland_checked_out():
    sample = FARMLAND / "heights" / "map.r16"
    return sample.is_file() and not sample.read_bytes()[:64].startswith(v.LFS_POINTER)


@pytest.mark.skipif(not _farmland_checked_out(), reason="maps/farmland's LFS files are not checked out")
def test_the_farmland_package_passes():
    errors, summary = v.validate(FARMLAND)
    assert errors == [], errors[:20]
    manifest = json.loads((FARMLAND / "map.json").read_text(encoding="utf-8"))
    assert [t["ring"] for t in manifest["tiles"]].count("surround") == 8
    assert [t["ring"] for t in manifest["tiles"]].count("far") == 8
    outlines = json.loads((FARMLAND / "outlines.json").read_text(encoding="utf-8"))
    assert [f["name"] for f in outlines["fields"]] == [f"F{i}" for i in range(1, 12)]
    probes = json.loads((FARMLAND / "height_probes.json").read_text(encoding="utf-8"))["probes"]
    assert len(probes) == 1000 and {p["ring"] for p in probes} == {"map", "surround", "far"}


def test_the_validator_refuses_a_folder_without_a_manifest(tmp_path):
    errors, _ = v.validate(tmp_path)
    assert errors == [f"{tmp_path}: no map.json"]
