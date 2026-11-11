"""The vendor plants' physics (w4a-farmland-vegetation 1.1): lib.vendor's readers on small synthetic inputs, lib.plant's
skeleton, shell and porosity on shapes whose answers are known, and the committed metadata of every variant of
assets-src/vendor_plants.json (schema 1, its params' hash, its .meta). A rebuild from the local store, which only a
checkout with vendor/ linked can run, must give the committed text byte for byte."""
import json
import math
import os

import pytest

from lib import catalog, meta, plant, vendor

CATALOG = os.path.join("assets-src", "vendor_plants.json")


def _tube(points_along, radius, sides=12):
    """A tube mesh round a polyline: rings of `sides` vertices, quads as two triangles each."""
    vertices, tris = [], []
    for k, p in enumerate(points_along):
        a = points_along[min(k + 1, len(points_along) - 1)]
        b = points_along[max(k - 1, 0)]
        t = [a[i] - b[i] for i in range(3)]
        n = math.sqrt(sum(x * x for x in t))
        t = [x / n for x in t]
        helper = (1.0, 0.0, 0.0) if abs(t[0]) < 0.9 else (0.0, 0.0, 1.0)
        u = [t[1] * helper[2] - t[2] * helper[1], t[2] * helper[0] - t[0] * helper[2], t[0] * helper[1] - t[1] * helper[0]]
        un = math.sqrt(sum(x * x for x in u))
        u = [x / un for x in u]
        v = [t[1] * u[2] - t[2] * u[1], t[2] * u[0] - t[0] * u[2], t[0] * u[1] - t[1] * u[0]]
        for s in range(sides):
            a_ = 2 * math.pi * s / sides
            vertices.append(tuple(p[i] + radius * (math.cos(a_) * u[i] + math.sin(a_) * v[i]) for i in range(3)))
    for k in range(len(points_along) - 1):
        for s in range(sides):
            a, b = k * sides + s, k * sides + (s + 1) % sides
            c, d = a + sides, b + sides
            tris += [(a, b, d), (a, d, c)]
    return vertices, tris


def test_a_bent_tube_becomes_capsules_on_its_axis():
    axis = [(0.3 * math.sin(y / 2.0), y, 0.0) for y in [i * 0.25 for i in range(25)]]  # 6 m, bending 0.3 m
    vertices, tris = _tube(axis, 0.08)
    points, welded = plant.weld(vertices, tris)
    nodes, vertex_node = plant.skeleton(points, welded, 0.1)
    members = plant.members(nodes)
    assert len(members) == 1 and members[0]["free_tip"]
    capsules = plant.fit_capsules(nodes, members[0], 2.0, 0.005, 0.02)
    assert 3 <= len(capsules) <= 12
    for a, b, r in capsules:
        assert abs(r - 0.08) < 0.01
        for p in (a, b):
            assert min(plant.segment_distance(p, a_, b_) for a_, b_ in zip(axis, axis[1:])) < 0.015
    assert set(vertex_node) == set(range(len(points)))


def test_a_thin_member_is_a_twig():
    vertices, tris = _tube([(0.0, y * 0.1, 0.0) for y in range(10)], 0.003, sides=6)
    points, welded = plant.weld(vertices, tris)
    nodes, _ = plant.skeleton(points, welded, 0.1)
    assert plant.fit_capsules(nodes, plant.members(nodes)[0], 2.0, 0.005, 0.02) == []


def test_a_round_capsule_takes_the_mean_distance_and_reports_the_worst():
    ring = [(0.6 * math.cos(k / 40 * 2 * math.pi), y, 0.25 * math.sin(k / 40 * 2 * math.pi))
            for k in range(40) for y in (0.0, 0.5, 1.0)]  # an ellipse 1.2 x 0.5 m round a vertical axis
    (a, b, r), residual = plant.round_capsule(ring, (0.0, 0.0, 0.0), (0.0, 1.0, 0.0), 0.005)
    dist = [math.hypot(x, z) for x, _, z in ring]
    assert abs(r - sum(dist) / len(dist)) < 1e-9 and abs(residual - max(abs(d - r) for d in dist)) < 1e-9


def test_the_shell_holds_every_sample_and_keeps_its_standoff():
    samples = [(x * 0.04, 1.0 + y * 0.04, 0.0) for x in range(30) for y in range(30)]  # a 1.2 m card, drawn whole
    pieces, info = plant.shell(samples, 0.2, 0.1, 1.0)
    assert pieces and info["uncovered"] == 0
    for p in samples:
        assert any(math.dist(p, c) <= r + 0.1 + plant.CELL_M for c, r in pieces)
    worst, _ = plant.standoff(pieces, samples, plant.icosphere_directions(2))
    assert worst <= 0.2 + plant.CELL_M * math.sqrt(3)


def test_porosity_of_a_full_and_an_empty_box():
    lo, hi = (0.0, 0.0, 0.0), (1.0, 1.0, 1.0)
    full = [(x * 0.02, y * 0.02, z * 0.02) for x in range(51) for y in range(51) for z in range(51)]
    beta, shares = plant.porosity(full, [], [], lo, hi)
    assert all(s > 0.97 for s in shares)  # every bearing's frontal rectangle covered
    assert abs(beta - (1 - (1 + 2 ** 0.5) / 2 / (4 / math.pi))) < 0.03  # MapWind's mean width 4/pi, not the 4 shares
    beta, _ = plant.porosity([], [], [], lo, hi)
    assert beta == 1.0


def _texture_meta(path, alpha_usage=1):
    """The import settings lib.vendor.texture reads beside a texture: Unity's alphaUsage (0 none, 1 from input)."""
    with open(str(path) + ".meta", "w", encoding="utf-8") as f:
        f.write(f"fileFormatVersion: 2\nTextureImporter:\n  alphaUsage: {alpha_usage}\n  grayScaleToAlpha: 0\n")


def test_alpha_reads_rows_from_the_top_of_an_image(tmp_path):
    from PIL import Image
    image = Image.new("RGBA", (4, 4), (0, 0, 0, 0))
    image.putpixel((0, 0), (255, 255, 255, 255))  # the top-left texel: u near 0, v near 1
    path = tmp_path / "a.png"
    image.save(path)
    _texture_meta(path)
    alpha = vendor.Alpha(str(path), 0.5)
    assert alpha.opaque(0.1, 0.9) and not alpha.opaque(0.1, 0.1)


def _rgbx_tiff(path, bits):
    """An uncompressed 4 x 4 TIFF of 4 samples whose 4th is 'unspecified' (ExtraSamples 0), as Megascans write their
    Albedo_Opacity maps: the 4th sample full at the top-left texel, zero elsewhere; 8 or 16 bits per sample."""
    import struct
    full, fmt = (255, "B") if bits == 8 else (65535, "H")
    data = b"".join(struct.pack("<4" + fmt, 9, 9, 9, full if i == 0 else 0) for i in range(16))
    tags = [(256, 3, 1, 4), (257, 3, 1, 4), (258, 3, 4, 8), (259, 3, 1, 1), (262, 3, 1, 2), (273, 4, 1, 0),
            (277, 3, 1, 4), (278, 3, 1, 4), (279, 4, 1, len(data)), (284, 3, 1, 1), (338, 3, 1, 0)]
    ifd = 8 + 8  # the header, then the four BitsPerSample shorts the 258 entry points to
    offset = ifd + 2 + 12 * len(tags) + 4
    body = struct.pack("<2sHI", b"II", 42, ifd) + struct.pack("<4H", bits, bits, bits, bits)
    body += struct.pack("<H", len(tags)) + b"".join(
        struct.pack("<HHI", tag, kind, count) + (struct.pack("<I", offset if tag == 273 else value) if kind == 4
                                                 or tag == 258 else struct.pack("<HH", value, 0))
        for tag, kind, count, value in tags) + struct.pack("<I", 0)
    path.write_bytes(body + data)


@pytest.mark.parametrize("bits", [8, 16])
def test_a_tiff_whose_fourth_sample_is_unspecified_reads_it_as_alpha(tmp_path, bits):
    # Pillow opens such a file as RGB and drops the 4th sample; Unity imports it as alpha (alphaUsage 1), and the
    # friend's leaf shader clips on it, so the samples must too (w4a review E5).
    from PIL import Image
    path = tmp_path / f"bush_{bits}.tif"
    _rgbx_tiff(path, bits)
    assert Image.open(path).mode == "RGB"
    _texture_meta(path)
    image, bottom_up = vendor.texture(str(path))
    assert image.mode == "RGBA" and not bottom_up
    assert image.getpixel((0, 0))[3] == 255 and image.getpixel((1, 0))[3] == 0
    alpha = vendor.Alpha(str(path), 0.5)
    assert alpha.opaque(0.1, 0.9) and not alpha.opaque(0.6, 0.9) and not alpha.opaque(0.1, 0.1)


def test_a_clipping_material_on_a_texture_without_alpha_is_refused(tmp_path):
    from PIL import Image
    rgb = tmp_path / "rgb.png"
    Image.new("RGB", (4, 4)).save(rgb)
    _texture_meta(rgb)
    rgba = tmp_path / "rgba.png"
    Image.new("RGBA", (4, 4)).save(rgba)
    _texture_meta(rgba, alpha_usage=0)  # the file has alpha, the import takes none
    for path in (rgb, rgba):
        with pytest.raises(vendor.VendorError, match="gives no alpha"):
            vendor.Alpha(str(path), 0.5)
        assert vendor.Alpha(str(path), None).opaque(0.5, 0.5)  # a material that does not clip draws whole


def test_a_unity_yaml_mesh_reads_its_positions_uvs_and_submeshes(tmp_path):
    import struct
    data = b"".join(struct.pack("<3f2f", *v) for v in [(0, 0, 0, 0, 0), (1, 0, 0, 1, 0), (0, 1, 0, 0, 1)])
    indices = struct.pack("<3H", 0, 1, 2)
    channel = "    - stream: 0\n      offset: {o}\n      format: 0\n      dimension: {d}\n"
    channels = "".join(channel.format(o=o, d=d) for o, d in ((0, 3), (0, 0), (0, 0), (0, 0), (12, 2)))
    text = ("%YAML 1.1\n--- !u!43 &4300000\nMesh:\n  m_Name: probe\n  m_SubMeshes:\n  - serializedVersion: 2\n"
            "    firstByte: 0\n    indexCount: 3\n    topology: 0\n    baseVertex: 0\n  m_IndexFormat: 0\n"
            f"  m_IndexBuffer: {indices.hex()}\n  m_VertexData:\n    m_VertexCount: 3\n    m_Channels:\n{channels}"
            f"    m_DataSize: {len(data)}\n    _typelessdata: {data.hex()}\n")
    path = tmp_path / "probe.asset"
    path.write_text(text, encoding="utf-8")
    vertices, tris, uvs = vendor.read_mesh(str(path), 4300000)
    assert vertices == [(0.0, 0.0, 0.0), (1.0, 0.0, 0.0), (0.0, 1.0, 0.0)]
    assert tris == [(0, 1, 2, 0)] and uvs == [((0.0, 0.0), (1.0, 0.0), (0.0, 1.0))]


def _variants(repo_root):
    with open(repo_root / CATALOG, encoding="utf-8") as f:
        cat = json.load(f)
    return [(asset_id, entry, v) for asset_id, entry in sorted(cat["assets"].items()) for v in entry["variants"]]


def test_every_variant_has_valid_committed_metadata(repo_root, material_ids):
    from vendor_plants import MODELS  # the writer's own layout
    seen = set()
    for asset_id, entry, variant in _variants(repo_root):
        assert variant not in seen
        seen.add(variant)
        params = json.loads((repo_root / "assets-src" / entry["class"] / f"{variant}.json").read_text(encoding="utf-8"))
        path = repo_root / MODELS / entry["class"] / asset_id / f"{variant}.assetmeta.json"
        data = json.loads(path.read_text(encoding="utf-8"))
        assert meta.validate(data, material_ids) == [], variant
        assert (data["id"], data["variant"], data["generator"]) == (asset_id, variant, "vendor_plant")
        assert data["params_sha256"] == catalog.params_sha256(params), f"{variant}: rebuild after a params edit"
        assert path.with_name(path.name + ".meta").is_file() and path.parent.with_name(asset_id + ".meta").is_file()
        assert any(s["material"] == "twig" for s in data["collision"]), f"{variant}: no shell"
        assert data["wind_volumes"] and 0 <= data["wind_volumes"][0]["porosity"] <= 1
        assert data["snag"] and not data["gaps"] and not any(s["landable"] for s in data["collision"])
        boxes = [(tuple(s["center"][a] - s["radius"] for a in range(3)), tuple(s["center"][a] + s["radius"] for a in range(3)))
                 for s in data["collision"] if s["material"] == "twig"]
        assert plant.most_in_cube(boxes)[0] <= 64, variant


def test_a_rebuild_from_the_store_gives_the_committed_text(repo_root):
    if not (repo_root / "vendor" / "world_final").is_dir():
        pytest.skip("the local store is not linked (pwsh tools/vendor/link.ps1)")
    import vendor_plants
    cat = vendor_plants.load_catalog(str(repo_root))
    (asset_id, entry, variant), = vendor_plants.selected(cat, "SmallBushDry_A")
    params = json.loads((repo_root / "assets-src" / entry["class"] / f"{variant}.json").read_text(encoding="utf-8"))
    data, _, _ = vendor_plants.build(str(repo_root), asset_id, entry, variant, params)
    committed = (repo_root / vendor_plants.MODELS / entry["class"] / asset_id / f"{variant}.assetmeta.json")
    assert meta.dumps(data) == committed.read_text(encoding="utf-8")
