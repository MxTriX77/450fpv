"""A glTF 2.0 binary (.glb) reader in plain Python, for the Godot stand-ins (p6-test-range design 2): the buffers
read as they are, so the port's only frame change is lib.godot's, and the tests run without Blender.

It reads what the first attempt's exports hold (Khronos glTF Blender I/O): root nodes with a mesh and no transform,
indexed triangle primitives with POSITION, NORMAL and TEXCOORD_0 floats, and plain PBR materials. Anything else
(a node transform, a child node, sparse or normalised accessors, another primitive mode) is refused, named, rather
than read wrong."""
import json
import struct

MAGIC = b"glTF"
JSON_CHUNK, BIN_CHUNK = b"JSON", b"BIN\x00"
COMPONENTS = {5121: ("B", 1), 5123: ("H", 2), 5125: ("I", 4), 5126: ("f", 4)}
WIDTH = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}
TRIANGLES = 4
TRANSFORM_KEYS = ("matrix", "translation", "rotation", "scale")


class GlbError(ValueError):
    """A file this reader does not take; the message names why."""


def read(data):
    """{"json": the glTF JSON, "bin": the binary chunk} of a .glb file's bytes."""
    if len(data) < 12 or data[:4] != MAGIC:
        raise GlbError("not a binary glTF file (no 'glTF' magic)")
    version, length = struct.unpack_from("<II", data, 4)
    if version != 2 or length != len(data):
        raise GlbError(f"glTF version {version}, length {length} for {len(data)} bytes; expected version 2")
    chunks, offset = {}, 12
    while offset < len(data):
        size, kind = struct.unpack_from("<I4s", data, offset)
        chunks[kind] = data[offset + 8:offset + 8 + size]
        offset += 8 + size
    if JSON_CHUNK not in chunks:
        raise GlbError("no JSON chunk")
    return {"json": json.loads(chunks[JSON_CHUNK].decode("utf-8")), "bin": chunks.get(BIN_CHUNK, b"")}


def materials(model):
    """Every material in file order: {"name", "base_color" (linear RGBA), "roughness", "double_sided"}."""
    out = []
    for i, m in enumerate(model["json"].get("materials", [])):
        pbr = m.get("pbrMetallicRoughness", {})
        if "baseColorTexture" in pbr or "normalTexture" in m:
            raise GlbError(f"material {i} ({m.get('name')}) has a texture; this reader takes flat materials only")
        out.append({"name": m.get("name", f"material_{i}"), "base_color": list(pbr.get("baseColorFactor", [1, 1, 1, 1])),
                    "roughness": pbr.get("roughnessFactor", 1.0), "double_sided": bool(m.get("doubleSided", False))})
    return out


def node_meshes(model):
    """{node name: [primitive...]} for the scene's root nodes, each primitive {"material": its name, "positions",
    "normals", "uvs" (lists of tuples, glTF's own frame and UV convention), "indices" (a flat list, three per
    triangle)}."""
    js = model["json"]
    names = [m["name"] for m in materials(model)]
    scene = js["scenes"][js.get("scene", 0)]
    out = {}
    for n in scene["nodes"]:
        node = js["nodes"][n]
        name = node.get("name", f"node_{n}")
        if any(k in node for k in TRANSFORM_KEYS) or node.get("children"):
            raise GlbError(f"node {name} has a transform or children; this reader takes root meshes at the origin")
        if "mesh" not in node:
            raise GlbError(f"node {name} has no mesh")
        prims = []
        for p in js["meshes"][node["mesh"]]["primitives"]:
            if p.get("mode", TRIANGLES) != TRIANGLES or "indices" not in p or "material" not in p:
                raise GlbError(f"node {name}: a primitive that is not indexed triangles with a material")
            attributes = p["attributes"]
            prims.append({"material": names[p["material"]],
                          "positions": _accessor(model, attributes["POSITION"], "VEC3"),
                          "normals": _accessor(model, attributes["NORMAL"], "VEC3"),
                          "uvs": _accessor(model, attributes["TEXCOORD_0"], "VEC2"),
                          "indices": _accessor(model, p["indices"], "SCALAR")})
        out[name] = prims
    return out


def _accessor(model, index, kind):
    js, data = model["json"], model["bin"]
    a = js["accessors"][index]
    if a["type"] != kind or "sparse" in a or a.get("normalized") or a["componentType"] not in COMPONENTS:
        raise GlbError(f"accessor {index}: expected a plain {kind}, got {a['type']} componentType {a['componentType']}")
    view = js["bufferViews"][a["bufferView"]]
    fmt, size = COMPONENTS[a["componentType"]]
    width = WIDTH[kind]
    stride = view.get("byteStride", size * width)
    base = view.get("byteOffset", 0) + a.get("byteOffset", 0)
    item = struct.Struct("<" + fmt * width)
    values = [item.unpack_from(data, base + k * stride) for k in range(a["count"])]
    return [v[0] for v in values] if width == 1 else values
