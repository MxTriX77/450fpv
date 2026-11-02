"""The Godot stand-ins Z01-Z13 (p6-test-range design 1-5): the frame change against independent rotation matrices,
the Godot catalog's rules (gaps, wind volumes, materials), the glb reader, the params and the stand-in audit, all in
plain Python; with the Godot ref present, the house's mesh is not mirrored and every catalog entry, params file and
palette recipe agrees with its Godot source; with the stand-ins built, the spec's scenarios "The metadata survives
the port" and "The door and gate are gaps" against the committed metadata."""
import copy
import json
import math
import random
import struct
from pathlib import Path

import pytest

from generators import godot_port
from lib import audit, catalog, glb, godot, meta, params
from lib.geometry import Mesh

S = (1.0, 1.0, -1.0)  # the mirror Godot -> asset space
MM, DEG = 0.001, 0.01  # the scenario's tolerances
IDS = [f"Z{n:02d}" for n in range(1, 14)]


def _rx(a):
    c, s = math.cos(a), math.sin(a)
    return [[1, 0, 0], [0, c, -s], [0, s, c]]


def _ry(a):
    c, s = math.cos(a), math.sin(a)
    return [[c, 0, s], [0, 1, 0], [-s, 0, c]]


def _rz(a):
    c, s = math.cos(a), math.sin(a)
    return [[c, -s, 0], [s, c, 0], [0, 0, 1]]


def _mul(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)] for i in range(3)]


def _godot_matrix(rotation_deg):
    """Godot's R = Ry(yaw) Rx(pitch) Rz(roll), from game/maps/README.md, by matrices."""
    yaw, pitch, roll = (math.radians(v) for v in rotation_deg)
    return _mul(_mul(_ry(yaw), _rx(pitch)), _rz(roll))


def _quat_matrix(q):
    x, y, z, w = q
    return [[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
            [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
            [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]]


def _angle_between(a, b):
    """The angle in degrees of the rotation a^T b, from ||a - b|| = 2 sqrt(2) sin(angle / 2), which stays exact for
    small angles where acos of the trace does not (a quaternion rounded to the micrometre read 0.08 degrees)."""
    norm = math.sqrt(sum((a[i][j] - b[i][j]) ** 2 for i in range(3) for j in range(3)))
    return math.degrees(2 * math.asin(min(1.0, norm / (2 * math.sqrt(2)))))


def _mirrored(m):
    return [[S[i] * m[i][j] * S[j] for j in range(3)] for i in range(3)]


def _point(p):
    return [p[0], p[1], -p[2]]


# --- the frame change, without the Godot ref ----------------------------------------------------------------------

def test_a_rotation_is_mirrored_as_s_r_s():
    rng = random.Random(7)
    for _ in range(500):
        angles = [rng.uniform(-360, 360) for _ in range(3)]
        q = godot.quaternion(angles)
        assert abs(math.sqrt(sum(c * c for c in q)) - 1) < 1e-12 and q[3] >= 0
        got, want = _quat_matrix(q), _mirrored(_godot_matrix(angles))
        assert max(abs(got[i][j] - want[i][j]) for i in range(3) for j in range(3)) < 1e-12


def test_a_pitched_rafter_keeps_its_lean_toward_the_ridge():
    """The house's north rafters, [0, -38.636, 0] at Godot z = -1.47: their long axis rises toward Godot +Z (the ridge
    at z = 0), so in asset space, where they stand at z = +1.47, it rises toward -Z, still the ridge. A box's axis is
    a line: S R S turns local +Z into minus the mirrored Godot axis, the same line."""
    m = _quat_matrix(godot.quaternion([0.0, -38.636, 0.0]))
    along = [m[i][2] for i in range(3)]
    assert abs(along[1]) > 0.6 and along[1] * along[2] < 0
    up = [m[i][1] for i in range(3)]  # a capsule's or cylinder's +Y keeps its sense: S leaves Y alone
    assert up[1] > 0.75 and up[2] > 0.6  # Godot's +Y leans to -Z (y' = cos, z' = sin(-38.6)), mirrored to +Z


def test_shapes_carry_their_fields_and_the_entry_material():
    entry = {"material": "timber", "wind_porosity": 0.5, "collision": [
        {"shape": "box", "size_m": [1, 2, 3], "position_m": [1, 2, 3]},
        {"shape": "capsule", "radius_m": 0.1, "height_m": 1.0, "position_m": [0, 1, 2], "rotation_deg": [10, 20, 30],
         "material": "masonry"},
        {"shape": "sphere", "radius_m": 0.4}]}
    box, capsule, sphere = godot.collision(entry)
    assert box == {"shape": "box", "center": [1.0, 2.0, -3.0], "size": [1.0, 2.0, 3.0], "rotation": [0.0, 0.0, 0.0, 1.0],
                   "material": "timber", "landable": False}
    assert list(capsule) == ["shape", "center", "radius", "height", "rotation", "material", "landable"]
    assert capsule["material"] == "masonry" and capsule["center"] == [0.0, 1.0, -2.0]
    assert sphere == {"shape": "sphere", "center": [0.0, 0.0, -0.0], "radius": 0.4, "material": "timber",
                      "landable": False}


def test_wind_volumes_follow_the_godot_rules():
    """Without a wind_volume the collision shapes are the volume; a porosity of 1 blocks nothing, so none is written."""
    shapes = [{"shape": "box", "size_m": [1, 1, 1], "position_m": [0, 0.5, 0]}]
    assert [v["porosity"] for v in godot.wind_volumes({"wind_porosity": 0.0, "collision": shapes})] == [0.0]
    assert godot.wind_volumes({"wind_porosity": 1.0, "collision": shapes}) == []
    crown = {"shape": "cylinder", "radius_m": 3.0, "height_m": 4.0, "position_m": [0, 4, 0]}
    (volume,) = godot.wind_volumes({"wind_porosity": 0.62, "collision": shapes, "wind_volume": [crown]})
    assert volume["shape"] == "cylinder" and volume["porosity"] == 0.62 and "material" not in volume


@pytest.mark.parametrize("yaw, across, normal", [(0.0, (1, 0, 0), (0, 0, -1)), (90.0, (0, 0, 1), (1, 0, 0)),
                                                 (180.0, (-1, 0, 0), (0, 0, 1))])
def test_a_gap_is_its_rectangle_turned_by_its_yaw(yaw, across, normal):
    g = godot.gap({"name": "w", "center_m": [5.0, 1.45, 2.0], "width_m": 0.8, "height_m": 1.0, "yaw_deg": yaw})
    centre = [sum(p[a] for p in g["polygon"]) / 4 for a in range(3)]
    assert all(abs(c - e) < 1e-12 for c, e in zip(centre, [5.0, 1.45, -2.0]))
    assert all(abs(n - e) < 1e-12 for n, e in zip(g["normal"], normal)) and g["narrowest_m"] == 0.8
    first, second, third = g["polygon"][:3]
    width = [second[a] - first[a] for a in range(3)]
    assert all(abs(w - 0.8 * e) < 1e-12 for w, e in zip(width, across))
    assert abs(third[1] - second[1] - 1.0) < 1e-12


# --- the glb reader -------------------------------------------------------------------------------------------------

def _tiny_glb(node_extra=None):
    positions = struct.pack("<9f", 0, 0, 0, 1, 0, 0, 0, 1, 0)
    normals = struct.pack("<9f", *([0, 0, 1] * 3))
    uvs = struct.pack("<6f", 0, 0, 1, 0, 0, 1)
    indices = struct.pack("<3H", 0, 1, 2) + b"\0\0"
    data = positions + normals + uvs + indices
    views = [{"buffer": 0, "byteOffset": 0, "byteLength": 36}, {"buffer": 0, "byteOffset": 36, "byteLength": 36},
             {"buffer": 0, "byteOffset": 72, "byteLength": 24}, {"buffer": 0, "byteOffset": 96, "byteLength": 6}]
    js = {"asset": {"version": "2.0"}, "scene": 0, "scenes": [{"nodes": [0]}],
          "nodes": [dict({"mesh": 0, "name": "x_LOD0"}, **(node_extra or {}))],
          "meshes": [{"primitives": [{"attributes": {"POSITION": 0, "NORMAL": 1, "TEXCOORD_0": 2}, "indices": 3,
                                      "material": 0}]}],
          "materials": [{"name": "m", "doubleSided": True,
                         "pbrMetallicRoughness": {"baseColorFactor": [0.5, 0.25, 0.125, 1], "roughnessFactor": 0.8}}],
          "accessors": [{"bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3"},
                        {"bufferView": 1, "componentType": 5126, "count": 3, "type": "VEC3"},
                        {"bufferView": 2, "componentType": 5126, "count": 3, "type": "VEC2"},
                        {"bufferView": 3, "componentType": 5123, "count": 3, "type": "SCALAR"}],
          "bufferViews": views, "buffers": [{"byteLength": len(data)}]}
    text = json.dumps(js).encode()
    text += b" " * (-len(text) % 4)
    body = struct.pack("<I4s", len(text), b"JSON") + text + struct.pack("<I4s", len(data), b"BIN\0") + data
    return b"glTF" + struct.pack("<II", 2, 12 + len(body)) + body


def test_the_glb_reader_reads_a_triangle_and_its_material():
    model = glb.read(_tiny_glb())
    (prim,) = glb.node_meshes(model)["x_LOD0"]
    assert prim["indices"] == [0, 1, 2] and prim["positions"][1] == (1.0, 0.0, 0.0) and prim["uvs"][2] == (0.0, 1.0)
    assert glb.materials(model) == [{"name": "m", "base_color": [0.5, 0.25, 0.125, 1], "roughness": 0.8,
                                     "double_sided": True}]


def test_the_glb_reader_refuses_a_node_transform():
    with pytest.raises(glb.GlbError, match="transform"):
        glb.node_meshes(glb.read(_tiny_glb({"translation": [1, 0, 0]})))
    with pytest.raises(glb.GlbError, match="magic"):
        glb.read(b"nope" * 4)


def test_a_mirrored_triangle_keeps_its_front_and_its_v():
    (prim,) = glb.node_meshes(glb.read(_tiny_glb()))["x_LOD0"]
    mesh, skipped = godot_port._mesh([prim])
    (indices, outward, uvs, slot), = mesh.faces
    assert skipped == 0 and slot == "m" and outward == (0.0, 0.0, -1.0)  # the glTF front (+Z) mirrored
    assert uvs == [(0.0, 1.0), (1.0, 1.0), (0.0, 0.0)] and mesh.corner_normals[0][0] == (0.0, 0.0, -1.0)


# --- params and the audit -------------------------------------------------------------------------------------------

def _params(repo_root, asset_id):
    entry = catalog.load(repo_root)["assets"][asset_id]
    return entry, catalog.load_params(catalog.params_path(repo_root, entry, entry["variants"][0]))


@pytest.mark.parametrize("asset_id", IDS)
def test_the_params_conform(repo_root, asset_id):
    assert params.validate(godot_port, _params(repo_root, asset_id)[1]) == []


def test_the_defaults_are_z01s(repo_root):
    flat = params._flatten(_params(repo_root, "Z01")[1])
    assert {k: s["default"] for k, s in godot_port.PARAMS.items()} == flat


@pytest.mark.parametrize("edit, expected", [
    (lambda p: p["source"].update(commit="8340138"), "full 40-digit commit"),
    (lambda p: p["source"].update(model_sha256="xyz"), "64 hex digits"),
    (lambda p: p["lod"].update(switch_m=[0.0, 45.0, 30.0]), "rising from 0"),
    (lambda p: p["lod"].update(switch_m=[5.0]), "rising from 0"),
    (lambda p: p["lod"].update(cull_m=40.0), "beyond the last switch"),
    (lambda p: p.update(holes=[{"id": "h"}]), "must hold exactly"),
    (lambda p: p.update(holes="none"), "'holes' must be list"),
])
def test_bad_params_are_refused_named(repo_root, edit, expected):
    edited = copy.deepcopy(_params(repo_root, "Z01")[1])
    edit(edited)
    assert any(expected in p for p in params.validate(godot_port, edited))


def _box_mesh(lo, hi):
    mesh = Mesh()
    for corner in [(x, y, z) for x in (lo[0], hi[0]) for y in (lo[1], hi[1]) for z in (lo[2], hi[2])]:
        mesh.vertex(corner)
    mesh.face([0, 1, 3], (-1, 0, 0), "s")
    mesh.face([4, 5, 7], (1, 0, 0), "s")
    return mesh


def test_the_stand_in_audit_holds_shapes_to_the_bounds_and_an_upright_cylinder_exactly():
    mesh = _box_mesh((-1, 0, -1), (1, 3, 1))
    trunk = {"shape": "cylinder", "center": [0, 1.5, 0], "radius": 0.5, "height": 3.0, "rotation": [0, 0, 0, 1],
             "material": "timber", "landable": False}
    data = {"collision": [trunk], "gaps": []}
    problems, lines = audit.stand_in(data, mesh)
    assert problems == [] and "at +0.0000 m past them, every one within" in lines[0]
    data["collision"].append(dict(trunk, center=[0, 1.56, 0]))
    problems, _ = audit.stand_in(data, mesh)
    assert problems == ["collision[1] cylinder reaches 0.0600 m past the LOD0 bounds, tolerance 0.05 m"]


def test_the_stand_in_record_is_validated_and_written_last(k01_params, material_ids):
    from generators import debris_brick
    built = debris_brick.build(k01_params)
    plain = meta.assemble("K01", "K01_whole", "debris_brick", k01_params, built, {"brick": "brick_red"})
    assert "stand_in" not in plain and "stand_in" not in meta.dumps(plain)
    built["stand_in"] = {"source": {"commit": "8" * 40, "model": "m.glb", "model_sha256": "e" * 64, "catalog_key": "k"},
                         "snag_hazard": True}
    data = meta.assemble("K01", "K01_whole", "debris_brick", k01_params, built, {"brick": "brick_red"})
    assert meta.validate(data, material_ids) == [] and list(json.loads(meta.dumps(data)))[-1] == "stand_in"
    data["stand_in"]["snag_hazard"] = "yes"
    assert any("boolean snag_hazard" in p for p in meta.validate(data, material_ids))


# --- against the Godot source ---------------------------------------------------------------------------------------

@pytest.fixture(scope="module")
def godot_source():
    """(root, commit) when refs/godot/main's pinned commit and its LFS objects are here; skips otherwise."""
    root = Path(__file__).resolve().parents[3]  # tests take conftest's paths as fixtures, never by import
    commit = godot_port.PARAMS["source.commit"]["default"]
    try:
        godot.git(root, "cat-file", "-e", f"{commit}^{{commit}}")
    except godot.SourceError:
        pytest.skip("the Godot ref is not fetched (git fetch origin main:refs/godot/main)")
    return root, commit


def _model(godot_source, source):
    root, commit = godot_source
    try:
        return glb.read(godot.lfs_object(root, commit, source["model"], source["model_sha256"]))
    except godot.SourceError as e:
        pytest.skip(str(e))


@pytest.mark.parametrize("asset_id", IDS)
def test_the_catalog_names_the_params_source(repo_root, godot_source, asset_id):
    entry, p = _params(repo_root, asset_id)
    assert entry["stand_in"] is True and entry["generator"] == "godot_port" and entry["class"] == "standins"
    assert entry["source"] == {"ref": "refs/godot/main", "commit": p["source"]["commit"],
                               "model": p["source"]["model"], "catalog_key": p["source"]["catalog_key"]}
    assert entry["variants"] == [f"{asset_id}_{p['source']['catalog_key']}"]
    godot.catalog_entry(godot_source[0], godot_source[1], p["source"]["catalog_key"])


def _srgb8(c):
    return round(255 * (12.92 * c if c <= 0.0031308 else 1.055 * c ** (1 / 2.4) - 0.055))


@pytest.mark.parametrize("asset_id", IDS)
def test_every_slot_has_a_recipe_and_each_palette_is_the_godot_colour(repo_root, godot_source, asset_id):
    """Design 4: bark and brick take the existing recipes; every other slot a palette of the model's own colour
    (sRGB within one step), smoothness 1 - roughness, two-sided where the material is."""
    entry, p = _params(repo_root, asset_id)
    model = _model(godot_source, p["source"])
    materials = {m["name"]: m for m in glb.materials(model)}
    assert set(entry["materials"]) <= set(materials)
    for slot, visual in entry["materials"].items():
        recipe = json.loads((repo_root / catalog.RECIPES / f"{visual}.json").read_text(encoding="utf-8"))
        if not visual.startswith("godot_"):
            assert visual in ("bark_locust", "brick_red") and ("bark" in slot or slot == "brick_red")
            continue
        m = materials[slot]
        # ModelPostprocessor reads a texture named with one of these as a data map (a single red channel for the
        # first four): the Z01 build's timbers drew red from godot_timber_rough_house_albedo.png
        assert not any(t in visual for t in ("_rough", "_ao", "ambientocclusion", "_opacity", "_normal", "_nor_gl"))
        assert recipe["kind"] == "palette" and len(recipe["colours"]) == 1
        colour = next(iter(recipe["colours"].values()))
        got = [int(colour[i:i + 2], 16) for i in (1, 3, 5)]
        assert all(abs(g - _srgb8(c)) <= 1 for g, c in zip(got, m["base_color"][:3])), (slot, colour)
        assert abs(recipe["smoothness"] - (1 - m["roughness"])) < 0.006
        assert recipe.get("two_sided", False) == m["double_sided"]


def test_the_house_is_not_mirrored(repo_root, godot_source):
    """The frame scenario by the mesh alone: in asset space the door (Godot x 1.8, the +Z wall) opens the south wall,
    z = -3.0, east of the centre, and the wall is drawn west of it."""
    _, p = _params(repo_root, "Z01")
    built = godot_port.build(p)
    lod0 = built["lods"][0]

    def wall_at(x, y):
        for indices, _, _, _ in lod0.faces:
            a, b, c = (lod0.vertices[i] for i in indices)
            if all(abs(v[2] + 3.0) < 0.01 for v in (a, b, c)):
                d = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
                w0 = ((b[1] - c[1]) * (x - c[0]) + (c[0] - b[0]) * (y - c[1])) / d
                w1 = ((c[1] - a[1]) * (x - c[0]) + (a[0] - c[0]) * (y - c[1])) / d
                if w0 >= 0 and w1 >= 0 and w0 + w1 <= 1:
                    return True
        return False

    assert not wall_at(1.8, 0.9) and wall_at(0.3, 0.9) and wall_at(-1.8, 0.9)
    door = next(g for g in built["gaps"] if g["id"] == "door")
    assert all(abs(v[2] + 3.0) < 1e-9 for v in door["polygon"]) and abs(door["normal"][2]) == 1


# --- the spec's scenarios against the built stand-ins ---------------------------------------------------------------

def _built(repo_root, asset_id):
    entry, p = _params(repo_root, asset_id)
    variant = entry["variants"][0]
    path = repo_root / catalog.MODELS / entry["class"] / asset_id / f"{variant}.assetmeta.json"
    if not path.is_file():
        pytest.skip(f"{asset_id} has not been built")
    return p, json.loads(path.read_text(encoding="utf-8"))


def _close(a, b, tol=MM):
    return len(a) == len(b) and all(abs(x - y) <= tol for x, y in zip(a, b))


def _same_shape(ours, theirs):
    """One Godot shape against its asset-space record within 1 mm and 0.01 degrees, by matrices."""
    if ours["shape"] != theirs["shape"] or not _close(ours["center"], _point(theirs.get("position_m", [0, 0, 0]))):
        return False
    if ours["shape"] == "box":
        if not _close(ours["size"], theirs["size_m"]):
            return False
    elif abs(ours["radius"] - theirs["radius_m"]) > MM or abs(ours.get("height", 0) - theirs.get("height_m", 0)) > MM:
        return False
    if ours["shape"] == "sphere":
        return True
    return _angle_between(_quat_matrix(ours["rotation"]),
                          _mirrored(_godot_matrix(theirs.get("rotation_deg", [0, 0, 0])))) <= DEG


@pytest.mark.parametrize("asset_id", IDS)
def test_the_metadata_survives_the_port(repo_root, godot_source, asset_id):
    p, data = _built(repo_root, asset_id)
    g = godot.catalog_entry(godot_source[0], godot_source[1], p["source"]["catalog_key"])
    shapes = g.get("collision", [])
    assert len(data["collision"]) == len(shapes)
    for i, (ours, theirs) in enumerate(zip(data["collision"], shapes)):
        assert _same_shape(ours, theirs), f"collision[{i}]"
        assert ours["material"] == theirs.get("material", g["material"]) and ours["landable"] is False
    volumes = [] if g["wind_porosity"] >= 1 else g.get("wind_volume", shapes)
    assert len(data["wind_volumes"]) == len(volumes)
    for i, (ours, theirs) in enumerate(zip(data["wind_volumes"], volumes)):
        assert _same_shape(ours, theirs) and ours["porosity"] == g["wind_porosity"], f"wind_volumes[{i}]"
    assert data["stand_in"]["snag_hazard"] == g["snag_hazard"] and data["snag"] == []
    assert data["stand_in"]["source"] == p["source"]
    switches = [lod["switch_distance_m"] for lod in data["lods"]]
    if "lod_switch_m" in g:
        # Godot's switches; the cull at Godot's last or farther (p6-test-range 1.2: 600 m over the 512 m map)
        assert switches[1:] == g["lod_switch_m"][:-1] and data["cull_distance_m"] >= g["lod_switch_m"][-1]
    else:
        assert switches == [0.0]
    assert len(data["gaps"]) == len(g.get("gaps", []))


@pytest.mark.parametrize("asset_id", ["Z01", "Z02", "Z03", "Z05"])
def test_the_door_and_gate_are_gaps(repo_root, godot_source, asset_id):
    """Each Godot opening is a gap at its Godot centre (mirrored) and size within 1 mm, facing its wall."""
    p, data = _built(repo_root, asset_id)
    g = godot.catalog_entry(godot_source[0], godot_source[1], p["source"]["catalog_key"])
    assert g["gaps"]
    for ours, theirs in zip(data["gaps"], g["gaps"]):
        assert ours["id"] == theirs["name"]
        poly = ours["polygon"]
        centre = [sum(v[a] for v in poly) / len(poly) for a in range(3)]
        assert _close(centre, _point(theirs["center_m"])), ours["id"]
        width, height = math.dist(poly[0], poly[1]), math.dist(poly[1], poly[2])
        assert abs(width - theirs["width_m"]) <= MM and abs(height - theirs["height_m"]) <= MM, ours["id"]
        yaw = math.radians(theirs["yaw_deg"])
        assert _close(ours["normal"], _point([math.sin(yaw), 0.0, math.cos(yaw)]), 1e-6), ours["id"]


def test_the_cellar_shaft_cuts_the_ground(repo_root, godot_source):
    """Design 5: Z04's hole is the Godot band (1.5 m across, 3.5 m along the axis), over its cavity 0.9 m wide, at
    the cellar floor's depth, in E01's form."""
    _, data = _built(repo_root, "Z04")
    (hole,) = data["holes"]
    assert hole["band_width_m"] == 1.5 and hole["top_width_m"] == 0.9 and hole["depth_m"] == 1.8
    (x0, y0, z0), (x1, y1, z1) = hole["centre_line"]
    assert x0 == x1 == y0 == y1 == 0 and abs(z1 - z0) + hole["band_width_m"] == 3.5
