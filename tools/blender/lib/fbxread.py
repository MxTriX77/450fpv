"""Reads the mesh vertices of a binary FBX file (versions 7400 and 7500) without Blender: enough for build.py to
compare the metadata with what Unity will see, and for the tests to do the same on the committed file. Nothing else
of the format is interpreted."""
import struct
import zlib

from . import frame

MAGIC = b"Kaydara FBX Binary  \x00\x1a\x00"
SCALARS = {"Y": "<h", "C": "<?", "I": "<i", "F": "<f", "D": "<d", "L": "<q"}
ARRAYS = {"f": "f", "d": "d", "l": "q", "i": "i", "b": "?"}
LFS_POINTER = b"version https://git-lfs.github.com/spec/v1"


def is_lfs_pointer(path):
    with open(path, "rb") as f:
        return f.read(len(LFS_POINTER)) == LFS_POINTER


def parse(path):
    """The top-level nodes as (name, props, children) triples, and the file version."""
    with open(path, "rb") as f:
        data = f.read()
    if data[:len(MAGIC)] != MAGIC:
        raise ValueError(f"{path}: not a binary FBX file" + (" (a git-lfs pointer)" if is_lfs_pointer(path) else ""))
    version = struct.unpack_from("<I", data, len(MAGIC))[0]
    pos = len(MAGIC) + 4
    nodes = []
    while pos < len(data):
        node, pos = _read_node(data, pos, version)
        if node is None:
            break
        nodes.append(node)
    return nodes, version


def unity_bounds(path):
    """Per model name, the (min, max) bounds of its mesh as Unity imports the file: metres, X negated (lib.frame).
    Refuses a model carrying its own transform, because the export bakes everything into the vertices."""
    nodes, _ = parse(path)
    top = {name: (props, children) for name, props, children in nodes}
    unit_scale = _property(top[b"GlobalSettings"][1], b"UnitScaleFactor", 1.0)
    geometries, models = {}, {}
    for name, props, children in top[b"Objects"][1]:
        if name == b"Geometry":
            vertices = next(p[0] for n, p, _ in children if n == b"Vertices")
            geometries[props[0]] = vertices
        elif name == b"Model":
            models[props[0]] = props[1].split(b"\x00", 1)[0].decode("utf-8")
            for key in (b"Lcl Translation", b"Lcl Rotation", b"Lcl Scaling"):
                default = 1.0 if key == b"Lcl Scaling" else 0.0
                if any(v != default for v in _property(children, key, (default,) * 3)):
                    raise ValueError(f"{path}: {models[props[0]]} carries a {key.decode()}; the export must bake it")
    bounds = {}
    for name, props, _ in top[b"Connections"][1]:
        if name == b"C" and props[0] == b"OO" and props[1] in geometries and props[2] in models:
            vertices = geometries[props[1]]
            points = [frame.fbx_to_unity(vertices[i:i + 3], unit_scale) for i in range(0, len(vertices), 3)]
            bounds[models[props[2]]] = (tuple(min(p[a] for p in points) for a in range(3)),
                                        tuple(max(p[a] for p in points) for a in range(3)))
    return bounds


def _property(children, key, default):
    """The value(s) of a Properties70 entry, or `default` when the file omits it (the FBX default applies)."""
    for name, _, grandchildren in children:
        if name == b"Properties70":
            for n, props, _ in grandchildren:
                if n == b"P" and props[0] == key:
                    values = props[4:]
                    return values[0] if len(values) == 1 and not isinstance(default, tuple) else tuple(values)
    return default


def _read_node(data, pos, version):
    if version >= 7500:
        end, count, _ = struct.unpack_from("<QQQ", data, pos)
        pos += 24
    else:
        end, count, _ = struct.unpack_from("<III", data, pos)
        pos += 12
    name_length = data[pos]
    pos += 1
    if end == 0:
        return None, pos  # the null record that closes a list of children
    name = data[pos:pos + name_length]
    pos += name_length
    props = []
    for _ in range(count):
        code = chr(data[pos])
        pos += 1
        if code in SCALARS:
            props.append(struct.unpack_from(SCALARS[code], data, pos)[0])
            pos += struct.calcsize(SCALARS[code])
        elif code in ARRAYS:
            length, encoding, byte_count = struct.unpack_from("<III", data, pos)
            pos += 12
            raw = data[pos:pos + byte_count]
            pos += byte_count
            if encoding:
                raw = zlib.decompress(raw)
            props.append(struct.unpack(f"<{length}{ARRAYS[code]}", raw))
        elif code in "SR":
            length = struct.unpack_from("<I", data, pos)[0]
            pos += 4
            props.append(data[pos:pos + length])
            pos += length
        else:
            raise ValueError(f"unknown FBX property type {code!r} at byte {pos - 1}")
    children = []
    while pos < end:
        child, pos = _read_node(data, pos, version)
        if child is None:
            break
        children.append(child)
    return (name, props, children), end
