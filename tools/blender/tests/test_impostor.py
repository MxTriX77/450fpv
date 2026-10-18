"""lib.impostor and the impostor LOD of lib.meta (p1-v01-locust design decision 3; asset-pipeline "Baked impostor LOD"),
in plain Python: the design's numbers (8 views at 15 degrees, a 1k atlas), the switch distance from the params, the
views' axes, the card frame that holds every view, the crown the card's normal follows, the atlas layout, the hash of
the render's inputs, the slot materials as the importer picks them, the PNG's chunks, and the metadata's impostor LOD
(last before the cull, validated, left out of the FBX check). The render itself runs in test_build_blender.py."""
import copy
import json
import math
import struct
import zlib

import pytest

from generators import debris_brick
from lib import impostor, meta
from lib.geometry import Mesh

VISUALS = {"brick": "brick_red"}


def box_mesh(lo, hi, slot="wood"):
    """An axis-aligned box as a lib.geometry Mesh, one slot."""
    mesh = Mesh()
    corners = [(x, y, z) for x in (lo[0], hi[0]) for y in (lo[1], hi[1]) for z in (lo[2], hi[2])]
    index = [mesh.vertex(c) for c in corners]
    for axis in range(3):
        for side in (0, 1):
            face = [i for i, c in zip(index, corners) if c[axis] == (lo, hi)[side][axis]]
            normal = tuple(float(a == axis) * (1 if side else -1) for a in range(3))
            a, b, c, d = face
            mesh.face([a, b, d, c], normal, slot)
    return mesh


def flat_materials(mesh, tile=(1.0, 1.0)):
    return {slot: {"tile_m": list(tile)} for slot in mesh.slots()}


def test_the_design_numbers():
    """Design 3: 8 azimuths at 15 degrees into a 1k atlas with alpha (the spec: 1k at most)."""
    assert impostor.VIEWS == 8
    assert impostor.ELEVATION_DEG == 15.0
    assert impostor.SIZE_PX == 1024 == meta.IMPOSTOR_MAX_PX
    assert impostor.COLUMNS * impostor.ROWS >= impostor.VIEWS
    assert impostor.CELL_PX == (256, 512)


def test_switch_distance_comes_from_the_params():
    assert impostor.switch_distance({}) is None
    assert impostor.switch_distance({"impostor": False, "lod": {"impostor_switch_m": 230.0}}) is None
    assert impostor.switch_distance({"impostor": True, "lod": {"impostor_switch_m": 230}}) == 230.0
    with pytest.raises(ValueError, match="lod.impostor_switch_m"):
        impostor.switch_distance({"impostor": True, "lod": {"cull_m": 2500.0}})


@pytest.mark.parametrize("k", range(8))
def test_views_are_orthonormal_and_unmirrored(k):
    right, up, toward = impostor.basis(k)
    for a, b in ((right, up), (right, toward), (up, toward)):
        assert sum(x * y for x, y in zip(a, b)) == pytest.approx(0.0, abs=1e-12)
    for v in (right, up, toward):
        assert math.sqrt(sum(x * x for x in v)) == pytest.approx(1.0)
    # Unity's left-handed camera: right = up x forward, the forward pointing away from the camera
    forward = tuple(-c for c in toward)
    cross = (up[1] * forward[2] - up[2] * forward[1], up[2] * forward[0] - up[0] * forward[2],
             up[0] * forward[1] - up[1] * forward[0])
    assert cross == pytest.approx(right)
    azimuth = math.degrees(math.atan2(toward[0], toward[2])) % 360
    assert azimuth == pytest.approx(45.0 * k)
    assert math.degrees(math.asin(toward[1])) == pytest.approx(15.0)


def test_view_0_sees_plus_x_on_its_left_and_view_2_sees_plus_z_on_its_right():
    assert impostor.basis(0)[0] == pytest.approx((-1.0, 0.0, 0.0))
    assert impostor.basis(2)[0] == pytest.approx((0.0, 0.0, 1.0))


def test_the_card_holds_every_view_within_the_pad():
    mesh = box_mesh((0.5, 0.0, -1.0), (2.5, 6.0, 1.5))  # off the axis, so one side reaches farther
    card, _ = impostor.frame(mesh, set())
    scale = impostor.CELL_PX[0] / card["width_m"]
    cos_e = math.cos(math.radians(impostor.ELEVATION_DEG))
    assert card["height_m"] * cos_e * scale == pytest.approx(impostor.CELL_PX[1])
    pad = impostor.PAD_PX / scale
    us, vs = [], []
    for k in range(impostor.VIEWS):
        right, up, _ = impostor.basis(k)
        us += [sum(a * b for a, b in zip(p, right)) for p in mesh.vertices]
        vs += [sum(a * b for a, b in zip(p, up)) for p in mesh.vertices]
    assert max(abs(u) for u in us) <= card["width_m"] / 2 - pad + 1e-9
    assert min(vs) == pytest.approx(card["bottom_m"] * cos_e + pad)
    assert max(vs) <= (card["bottom_m"] + card["height_m"]) * cos_e - pad + 1e-9
    # one of the two dimensions is the binding one: the box fills the cell to the pad across or up
    assert (max(abs(u) for u in us) == pytest.approx(card["width_m"] / 2 - pad)
            or max(vs) == pytest.approx((card["bottom_m"] + card["height_m"]) * cos_e - pad))


def test_the_crown_follows_the_card_slots():
    mesh = box_mesh((-0.1, 0.0, -0.1), (0.1, 3.0, 0.1), "bark")
    leaves = box_mesh((1.0, 2.0, 1.0), (3.0, 4.0, 2.0), "leaf")
    offset = len(mesh.vertices)
    mesh.vertices += leaves.vertices
    mesh.faces += [([i + offset for i in f], n, uvs, s) for f, n, uvs, s in leaves.faces]
    _, crown = impostor.frame(mesh, {"leaf"})
    assert crown["centre"] == pytest.approx([2.0, 3.0, 1.5])
    assert crown["radii_m"] == pytest.approx([math.hypot(1.0, 0.5), 1.0])
    _, everything = impostor.frame(mesh, set())
    assert everything["centre"] == pytest.approx([1.45, 2.0, 0.95])
    assert everything["radii_m"][1] == pytest.approx(2.0)


def test_crown_slots_are_the_card_recipes():
    visuals = {"bark": "bark_locust", "twig": "bark_locust", "leaf": "leaf_locust", "pod": "pod_locust"}
    assert impostor.crown_slots(visuals, {"leaf_locust", "pod_locust", "leaf_shrub"}) == {"leaf", "pod"}


def test_layout_puts_each_view_in_its_cell_and_scales_the_uvs():
    mesh = box_mesh((0.5, 0.0, -1.0), (2.5, 6.0, 1.5))
    record = {"card": impostor.frame(mesh, set())[0]}
    vertices, faces, uvs, slots, side = impostor.layout(mesh, record, flat_materials(mesh, (2.0, 0.5)))
    card = record["card"]
    scale = impostor.CELL_PX[0] / card["width_m"]
    cell_w, cell_h = card["width_m"], impostor.CELL_PX[1] / scale
    assert side == pytest.approx(impostor.COLUMNS * cell_w) == pytest.approx(impostor.ROWS * cell_h)
    n, pad = len(mesh.vertices), impostor.PAD_PX / scale
    for k in range(impostor.VIEWS):
        x0, y0 = (k % impostor.COLUMNS) * cell_w, (impostor.ROWS - 1 - k // impostor.COLUMNS) * cell_h
        for x, y, _ in vertices[k * n:(k + 1) * n]:
            assert x0 + pad - 1e-9 <= x <= x0 + cell_w - pad + 1e-9
            assert y0 + pad - 1e-9 <= y <= y0 + cell_h - pad + 1e-9
    assert len(faces) == impostor.VIEWS * len(mesh.faces) and set(slots) == {"wood"}
    first = mesh.faces[0][2]
    assert uvs[0] == pytest.approx([(u / 2.0, v / 0.5) for u, v in first])


def test_inputs_hash_follows_the_mesh_the_recipe_and_the_map(tmp_path):
    mesh = box_mesh((0.0, 0.0, 0.0), (1.0, 2.0, 1.0))
    texture = tmp_path / "map.png"
    texture.write_bytes(b"one")
    materials = {"wood": {"visual": "v", "recipe": {"tint": [1.0, 1.0, 1.0, 1.0]}, "texture": str(texture)}}
    record = {"size_px": [1024, 1024], "views": 8, "columns": 4, "rows": 2, "elevation_deg": 15.0,
              "card": impostor.frame(mesh, set())[0], "crown": impostor.frame(mesh, set())[1]}
    first = impostor.inputs_sha256(mesh, record, materials)
    assert impostor.inputs_sha256(copy.deepcopy(mesh), copy.deepcopy(record), copy.deepcopy(materials)) == first
    moved = copy.deepcopy(mesh)
    moved.vertices[0] = (0.0, 0.0, 0.001)
    assert impostor.inputs_sha256(moved, record, materials) != first
    tinted = copy.deepcopy(materials)
    tinted["wood"]["recipe"]["tint"][0] = 0.9
    assert impostor.inputs_sha256(mesh, record, tinted) != first
    texture.write_bytes(b"two")
    assert impostor.inputs_sha256(mesh, record, materials) != first


def test_slot_materials_pick_the_importers_base_maps(repo_root):
    visuals = {"bark": "bark_locust", "leaf": "leaf_locust", "pod": "pod_locust", "stem": "bark_shrub"}
    got = impostor.slot_materials(str(repo_root), visuals, ["bark", "leaf", "pod", "stem"])
    name = {slot: m["texture"].replace("\\", "/").rsplit("/", 1)[-1] for slot, m in got.items()}
    assert name == {"bark": "bark_locust_base.png",     # a groove recipe: the derived base
                    "leaf": "leaf_locust_base.png",     # alpha-clipped, the set ships its opacity apart
                    "pod": "pod_locust_base.png",       # a painted recipe
                    "stem": "bark_brown_02_diff_1k.png"}  # the set's diffuse
    assert got["leaf"]["cutoff"] == 0.5 and got["bark"]["cutoff"] is None
    assert got["bark"]["mask"].replace("\\", "/").endswith("Art/Materials/bark_locust_mask.png")  # its occlusion
    assert got["pod"]["mask"] is None  # no mask: URP/Lit's occlusion is 1
    assert got["bark"]["tint"] == pytest.approx([impostor.gamma_to_linear(0.845), impostor.gamma_to_linear(0.928),
                                                 1.252 ** 2.2])
    assert got["bark"]["tile_m"] == [1.0, 1.0]


def test_gamma_to_linear_is_unitys():
    assert impostor.gamma_to_linear(0.0) == 0.0
    assert impostor.gamma_to_linear(0.04) == pytest.approx(0.04 / 12.92)
    assert impostor.gamma_to_linear(0.5) == pytest.approx(0.214041, abs=1e-6)
    assert impostor.gamma_to_linear(1.0) == 1.0
    assert impostor.gamma_to_linear(1.252) == pytest.approx(1.252 ** 2.2)


def png(chunks):
    """A PNG from (type, data) chunks."""
    out = [b"\x89PNG\r\n\x1a\n"]
    for kind, data in chunks:
        out.append(struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data)))
    return b"".join(out)


def test_strip_png_keeps_the_critical_chunks(tmp_path):
    header = struct.pack(">IIBBBBB", 2, 1, 8, 6, 0, 0, 0)
    pixels = zlib.compress(b"\x00" + bytes(8))
    path = tmp_path / "atlas.png"
    path.write_bytes(png([(b"IHDR", header), (b"tEXt", b"Date\x002026"), (b"IDAT", pixels), (b"tIME", bytes(7)),
                          (b"IEND", b"")]))
    impostor.strip_png(str(path))
    assert path.read_bytes() == png([(b"IHDR", header), (b"IDAT", pixels), (b"IEND", b"")])
    assert impostor.png_header(str(path)) == (2, 1, 8, 6)


def k01_with_impostor(params, switch=60.0):
    built = debris_brick.build(params)
    mesh = built["lods"][0]
    materials = {"brick": {"visual": "brick_red", "recipe": {}, "texture": __file__, "tile_m": [1.0, 1.0]}}
    lod = impostor.lod("K01_whole", switch, mesh, set(), materials)
    lod["impostor"]["atlas_sha256"] = "a" * 64
    return meta.assemble("K01", "K01_whole", "debris_brick", params, built, VISUALS, lod)


def test_impostor_lod_is_the_last_before_the_cull(k01_params, material_ids):
    data = k01_with_impostor(k01_params)
    assert meta.validate(data, material_ids) == []
    last = data["lods"][-1]
    assert [lod["mesh"] for lod in data["lods"]] == ["K01_whole_LOD0", "K01_whole_LOD1", "K01_whole_impostor"]
    assert last["switch_distance_m"] == 60.0 and data["cull_distance_m"] == 120.0
    assert last["tris"] == 2  # one quad
    record = last["impostor"]
    assert record["atlas"] == "K01_whole_impostor.png" and record["size_px"] == [1024, 1024]
    assert (record["views"], record["columns"], record["rows"], record["elevation_deg"]) == (8, 4, 2, 15.0)
    assert json.loads(meta.dumps(data))["lods"][-1] == last


def test_metadata_without_impostor_is_unchanged(k01_params):
    plain = meta.assemble("K01", "K01_whole", "debris_brick", k01_params, debris_brick.build(k01_params), VISUALS)
    assert meta.dumps(plain) == meta.dumps(meta.assemble("K01", "K01_whole", "debris_brick", k01_params,
                                                         debris_brick.build(k01_params), VISUALS, None))
    assert all("impostor" not in lod for lod in plain["lods"])


@pytest.mark.parametrize("edit, expected", [
    (lambda d: d["lods"].insert(1, d["lods"].pop()), "the impostor must be the last LOD, before the cull"),
    (lambda d: d["lods"][-1].update(switch_distance_m=2.0), "must start at 0 and increase"),
    (lambda d: d["lods"][-1].update(switch_distance_m=150.0), "cull_distance_m must lie beyond the last LOD switch"),
    (lambda d: d["lods"][-1]["impostor"].update(size_px=[2048, 2048]), "each at most 1024 px (1k)"),
    (lambda d: d["lods"][-1]["impostor"].update(atlas="K01_whole_impostor.jpg"), "atlas must name a .png"),
    (lambda d: d["lods"][-1]["impostor"].update(views=9), "views <= columns * rows"),
    (lambda d: d["lods"][-1].update(tris=32), "must be 2: the card is one quad"),
    (lambda d: d["lods"][-1]["impostor"]["crown"].update(radii_m=[0.0, 1.0]), "crown needs a centre"),
    (lambda d: d["lods"][-1]["impostor"]["crown"].update(occlusion=0.0), "occlusion in (0, 1]"),
    (lambda d: d["lods"][-1]["impostor"].update(atlas_sha256="none"), "atlas_sha256 is not a 64-digit hex"),
])
def test_validate_names_the_impostor_problem(k01_params, material_ids, edit, expected):
    data = k01_with_impostor(k01_params)
    edit(data)
    problems = meta.validate(data, material_ids)
    assert any(expected in p for p in problems), problems


def test_the_fbx_check_leaves_the_card_to_the_importer(k01_params, k01_out):
    data = k01_with_impostor(k01_params)
    assert meta.check_against_fbx(data, str(k01_out / "K01_whole.fbx")) == []


def test_recorded_atlas_is_up_to_date_only_with_both_hashes(tmp_path):
    atlas = tmp_path / "V_impostor.png"
    atlas.write_bytes(b"pixels")
    meta_path = tmp_path / "V.assetmeta.json"
    record = {"inputs_sha256": "1" * 64, "atlas_sha256": impostor.file_sha256(str(atlas)), "crown": {"occlusion": 0.8}}
    meta_path.write_text(json.dumps({"lods": [{"mesh": "V_LOD0"}, {"mesh": "V_impostor", "impostor": record}]}))
    assert impostor.recorded(str(meta_path), str(atlas), "1" * 64) == record  # with what its render measured
    assert impostor.recorded(str(meta_path), str(atlas), "2" * 64) is None  # the params or materials changed
    atlas.write_bytes(b"hand-edited")
    assert impostor.recorded(str(meta_path), str(atlas), "1" * 64) is None  # never hand-edited: rendered over
    assert impostor.recorded(str(tmp_path / "missing.json"), str(atlas), "1" * 64) is None
