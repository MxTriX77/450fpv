"""The local vendor store as a read-only mesh source (w4a-farmland-vegetation; D-089: the store's geometry never leaves
it), in plain Python: a Unity prefab's LOD meshes in the prefab's own frame, as Unity draws them.

The prefab (YAML): its LODGroup lists each LOD's renderers; a renderer's GameObject holds a MeshFilter naming a mesh by
(guid, fileID) and a Transform; the transforms from the renderer up to the prefab's root, the root's own excluded (a
terrain tree is drawn at its instance's position, yaw and scale, never its root's), make the mesh's matrix. The
renderer's m_Materials name a material per submesh. A guid is a file of the project by its .meta.

A mesh is either a sub-mesh of an FBX file (lib.fbxread's node tree) or a Unity YAML mesh asset (.asset).
- FBX: the .meta's fileIDToRecycleName (or internalIDToNameTable, its newer form) names the mesh of a fileID; a file
  whose table is empty names its meshes by hash, and the params name the mesh instead (`mesh`). Unity names a mesh after its Geometry node, or its Model when the Geometry has no name. Each
  polygon is fan-triangulated, its material index (LayerElementMaterial, ByPolygon or AllSame) its submesh. Unity
  bakes the Model's geometric transform (GeometricTranslation, GeometricRotation, GeometricScaling: v' = T R S v, the
  rotation Euler XYZ, Rz Ry Rx) into the vertices, converts the file's units (UnitScaleFactor / 100 when the import
  uses the file's scale, times the import's globalScale) and takes the right-handed file into its left-handed frame
  by negating X (lib.frame.fbx_to_unity). Every file read here is Y-up (UpAxis 1), the axis conversion Unity would
  otherwise add to the root node; another one is refused.
- YAML mesh: m_VertexData's position channel (channel 0, float32 x 3) in the stream-0 vertex layout, and
  m_IndexBuffer (UInt16 or UInt32 by m_IndexFormat) per m_SubMeshes entry (firstByte, indexCount, baseVertex),
  already in Unity's frame.
The prefab's LODGroup records m_LocalReferencePoint and m_Size, the centre and the largest side of the box round all
its renderers' bounds; `check_group` holds the meshes read here to them, so a wrong frame, unit or transform fails
there rather than in the physics."""
import hashlib
import math
import os
import re
import struct

from . import fbxread

DOC = re.compile(r"--- !u!(\d+) &(-?\d+)[^\n]*\n(.*?)(?=\n--- |\Z)", re.S)
TYPE_TRANSFORM, TYPE_MESH_FILTER, TYPE_LOD_GROUP, TYPE_GAME_OBJECT = "4", "33", "205", "1"
GROUP_TOLERANCE_M = 0.002  # the LOD group's centre and size against the meshes read here (Unity stores floats; a
                           # millimetre of rounding in the transforms, assumed)


class VendorError(RuntimeError):
    """The store is missing, a file differs from its pin, or a mesh cannot be read as pinned; the message names it."""


def sha256(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def pinned(path, digest):
    """`path`, after checking it exists and hashes to `digest` (a vendor file changed under the params is refused)."""
    if not os.path.isfile(path):
        raise VendorError(f"{path} is missing: link the local store with `pwsh tools/vendor/link.ps1` (D-089)")
    got = sha256(path)
    if got != digest:
        raise VendorError(f"{path} hashes to {got}, the params pin {digest}")
    return path


def guid_of(path):
    """The guid in the .meta beside a project file."""
    with open(path + ".meta", encoding="utf-8") as f:
        for line in f:
            if line.startswith("guid:"):
                return line.split()[1]
    raise VendorError(f"{path}.meta names no guid")


# --- the prefab -------------------------------------------------------------------------------------------------

def _docs(path):
    with open(path, encoding="utf-8") as f:
        return {m.group(2): (m.group(1), m.group(3)) for m in DOC.finditer(f.read())}


def _field(body, key):
    m = re.search(r"^\s*" + key + r": (.*)$", body, re.M)
    return m.group(1).strip() if m else None


def _vec(text, keys):
    values = dict(re.findall(r"(\w+): (-?[\d.eE+-]+)", text))
    return tuple(float(values[k]) for k in keys)


def prefab_lods(path):
    """The prefab's LOD group as {"reference": (x, y, z), "size": s, "lods": [[renderer...] per LOD]}, each renderer
    {"name", "mesh_guid", "mesh_file_id", "materials": [guid per submesh], "matrix": 3x4 rows}, the matrix taking the
    mesh's frame to the prefab's (its root's transform excluded)."""
    docs = _docs(path)
    groups = [body for kind, body in docs.values() if kind == TYPE_LOD_GROUP]
    if len(groups) != 1:
        raise VendorError(f"{path}: expected one LODGroup, found {len(groups)}")
    group = groups[0]
    lods = []
    for block in re.split(r"- screenRelativeHeight", group)[1:]:
        height = float(re.match(r": ([\d.eE+-]+)", block).group(1))
        renderers = []
        for file_id in re.findall(r"renderer: \{fileID: (-?\d+)\}", block):
            body = docs[file_id][1]
            game_object = docs[re.search(r"m_GameObject: \{fileID: (-?\d+)\}", body).group(1)][1]
            components = re.findall(r"component: \{fileID: (-?\d+)\}", game_object)
            filters = [c for c in components if docs[c][0] == TYPE_MESH_FILTER]
            transform = next(c for c in components if docs[c][0] == TYPE_TRANSFORM)
            mesh = re.search(r"m_Mesh: \{fileID: (-?\d+), guid: (\w+)", docs[filters[0]][1])
            materials = re.findall(r"- \{fileID: -?\d+, guid: (\w+)", body.split("m_Materials:")[1].split("m_")[0])
            renderers.append({"name": _field(game_object, "m_Name"), "mesh_guid": mesh.group(2),
                              "mesh_file_id": int(mesh.group(1)), "materials": materials,
                              "matrix": _chain(docs, transform)})
        lods.append({"screen_relative_height": height, "renderers": renderers})
    return {"reference": _vec(_field(group, "m_LocalReferencePoint"), "xyz"), "size": float(_field(group, "m_Size")),
            "lods": lods}


def _chain(docs, transform):
    """The matrix of a transform up to (not including) its root."""
    matrix = [[1.0, 0.0, 0.0, 0.0], [0.0, 1.0, 0.0, 0.0], [0.0, 0.0, 1.0, 0.0]]
    while True:
        body = docs[transform][1]
        father = re.search(r"m_Father: \{fileID: (-?\d+)\}", body).group(1)
        if father == "0":
            return matrix
        local = trs(_vec(_field(body, "m_LocalPosition"), "xyz"), _vec(_field(body, "m_LocalRotation"), "xyzw"),
                    _vec(_field(body, "m_LocalScale"), "xyz"))
        matrix = compose(local, matrix)
        transform = father


def trs(t, q, s):
    """The 3x4 matrix of a translation, a unit quaternion [x, y, z, w] and a scale, applied scale first."""
    x, y, z, w = q
    r = [[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
         [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
         [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]]
    return [[r[i][0] * s[0], r[i][1] * s[1], r[i][2] * s[2], t[i]] for i in range(3)]


def compose(a, b):
    """a after b, both 3x4."""
    return [[sum(a[i][k] * b[k][j] for k in range(3)) + (a[i][3] if j == 3 else 0.0) for j in range(4)]
            for i in range(3)]


def apply(m, p):
    return tuple(m[i][0] * p[0] + m[i][1] * p[1] + m[i][2] * p[2] + m[i][3] for i in range(3))


# --- the meshes -------------------------------------------------------------------------------------------------

def read_mesh(path, file_id, name=None):
    """(vertices, triangles, uvs) of a mesh as Unity imports it: vertices in the mesh's frame, triangles (i, j, k,
    submesh), and per triangle its corners' first UVs ((u, v) x 3). `name` is the FBX mesh's name when its .meta has
    no fileID table."""
    if path.lower().endswith(".fbx"):
        return _fbx_mesh(path, _fbx_mesh_name(path, file_id, name))
    if path.endswith(".asset"):
        return _yaml_mesh(path, file_id)
    raise VendorError(f"{path}: not an FBX file or a Unity mesh asset")


def _fbx_mesh_name(path, file_id, name):
    with open(path + ".meta", encoding="utf-8") as f:
        meta = f.read()
    table = meta.split("fileIDToRecycleName:")[1].split("\n  externalObjects")[0] if "fileIDToRecycleName:" in meta \
        else ""
    recycled = dict(re.findall(r"^\s+(\d+): (.+)$", table, re.M))
    # the newer form of the same table: "- first: {43: <fileID>} second: <name>" for each mesh (class 43)
    recycled.update(re.findall(r"- first:\s+43: (-?\d+)\s+second: (.+)", meta))
    if str(file_id) in recycled:
        found = recycled[str(file_id)].strip()
        if name is not None and name != found:
            raise VendorError(f"{path}: fileID {file_id} is the mesh {found!r}, the params name {name!r}")
        return found
    if name is None:
        raise VendorError(f"{path}: fileID {file_id} has no name in the .meta; name the mesh in the params")
    return name


def _import_scale(path):
    with open(path + ".meta", encoding="utf-8") as f:
        meta = f.read()
    global_scale = float(_field(meta, "globalScale") or 1.0)
    use_file_scale = (_field(meta, "useFileScale") or "1") == "1"
    return global_scale, use_file_scale


def _fbx_mesh(path, name):
    nodes, _ = fbxread.parse(path)
    top = {n: (p, c) for n, p, c in nodes}
    settings = top[b"GlobalSettings"][1]
    if fbxread._property(settings, b"UpAxis", 1) != 1:
        raise VendorError(f"{path}: not a Y-up file; Unity's axis conversion is not modelled here")
    unit = fbxread._property(settings, b"UnitScaleFactor", 1.0)
    global_scale, use_file_scale = _import_scale(path)
    k = global_scale * (unit / 100.0 if use_file_scale else 1.0)
    geometries, models = {}, {}
    for kind, props, children in top[b"Objects"][1]:
        label = props[1].split(b"\x00", 1)[0].decode("utf-8")
        if kind == b"Geometry":
            geometries[props[0]] = (label, children)
        elif kind == b"Model":
            models[props[0]] = (label, children)
    for kind, props, _ in top[b"Connections"][1]:
        if kind == b"C" and props[0] == b"OO" and props[1] in geometries and props[2] in models:
            geometry_name, children = geometries[props[1]]
            model_name, model_children = models[props[2]]
            if (geometry_name or model_name) == name:
                return _fbx_geometry(children, _geometric(model_children), k)
    raise VendorError(f"{path}: no mesh named {name!r}")


def _geometric(children):
    t = fbxread._property(children, b"GeometricTranslation", (0.0, 0.0, 0.0))
    r = fbxread._property(children, b"GeometricRotation", (0.0, 0.0, 0.0))
    s = fbxread._property(children, b"GeometricScaling", (1.0, 1.0, 1.0))
    rx, ry, rz = (math.radians(a) for a in r)
    mx = [[1, 0, 0, 0], [0, math.cos(rx), -math.sin(rx), 0], [0, math.sin(rx), math.cos(rx), 0]]
    my = [[math.cos(ry), 0, math.sin(ry), 0], [0, 1, 0, 0], [-math.sin(ry), 0, math.cos(ry), 0]]
    mz = [[math.cos(rz), -math.sin(rz), 0, 0], [math.sin(rz), math.cos(rz), 0, 0], [0, 0, 1, 0]]
    rotation = compose(mz, compose(my, mx))
    scale = [[s[0], 0, 0, 0], [0, s[1], 0, 0], [0, 0, s[2], 0]]
    m = compose(rotation, scale)
    for i in range(3):
        m[i][3] = t[i]
    return m


def _fbx_geometry(children, geometric, k):
    fields = {n: (p, c) for n, p, c in children}
    raw = fields[b"Vertices"][0][0]
    vertices = []
    for i in range(0, len(raw), 3):
        x, y, z = apply(geometric, raw[i:i + 3])
        vertices.append((-x * k, y * k, z * k))
    materials, mapping = (0,), b"AllSame"
    if b"LayerElementMaterial" in fields:
        layer = {n: p for n, p, _ in fields[b"LayerElementMaterial"][1]}
        mapping = layer[b"MappingInformationType"][0]
        materials = layer[b"Materials"][0]
    uv_of = _fbx_uvs(children)
    triangles, uvs, polygon, corners, count = [], [], [], [], 0
    for corner, index in enumerate(fields[b"PolygonVertexIndex"][0][0]):
        polygon.append(index if index >= 0 else ~index)
        corners.append(uv_of(corner, polygon[-1]))
        if index < 0:
            submesh = materials[count] if mapping == b"ByPolygon" else materials[0]
            for i in range(1, len(polygon) - 1):
                triangles.append((polygon[0], polygon[i], polygon[i + 1], submesh))
                uvs.append((corners[0], corners[i], corners[i + 1]))
            polygon, corners, count = [], [], count + 1
    return vertices, triangles, uvs


def _fbx_uvs(children):
    """The first UV layer as a function of (polygon-vertex index, vertex index) -> (u, v); (0, 0) without one."""
    layers = [c for n, p, c in children if n == b"LayerElementUV" and p and p[0] == 0]
    if not layers:
        return lambda corner, vertex: (0.0, 0.0)
    layer = {n: p for n, p, _ in layers[0]}
    values = layer[b"UV"][0]
    mapping = layer[b"MappingInformationType"][0]
    direct = layer[b"ReferenceInformationType"][0] == b"Direct"
    index = None if direct else layer[b"UVIndex"][0]

    def uv(corner, vertex):
        k = corner if mapping == b"ByPolygonVertex" else vertex
        i = k if direct else index[k]
        return (values[2 * i], values[2 * i + 1])

    return uv


def _yaml_mesh(path, file_id):
    docs = _docs(path)
    if str(file_id) not in docs:
        raise VendorError(f"{path}: no object {file_id}")
    body = docs[str(file_id)][1]
    data = bytes.fromhex(_field(body, "_typelessdata"))
    count = int(_field(body, "m_VertexCount"))
    channels = re.findall(r"- stream: (\d+)\s+offset: (\d+)\s+format: (\d+)\s+dimension: (\d+)", body)
    sizes = {0: 4, 1: 2, 2: 1, 3: 1, 4: 2, 5: 2}  # float32, float16, unorm8, snorm8, unorm16, snorm16
    stride = max(int(o) + sizes[int(f)] * (int(d) & 0xF) for s, o, f, d in channels if s == "0" and int(d) & 0xF)
    stream, offset, fmt, dim = (int(v) for v in channels[0])
    if (stream, fmt, dim & 0xF) != (0, 0, 3) or len(data) < stride * count:
        raise VendorError(f"{path}: positions are not float32 x 3 in stream 0")
    vertices = [struct.unpack_from("<3f", data, offset + i * stride) for i in range(count)]
    uv_stream, uv_offset, uv_format, uv_dim = (int(v) for v in channels[4])  # TexCoord0
    if (uv_stream, uv_format, uv_dim & 0xF) == (0, 0, 2):
        texcoords = [struct.unpack_from("<2f", data, uv_offset + i * stride) for i in range(count)]
    else:
        texcoords = [(0.0, 0.0)] * count
    wide = _field(body, "m_IndexFormat") == "1"
    indices = bytes.fromhex(_field(body, "m_IndexBuffer"))
    triangles = []
    for submesh, m in enumerate(re.finditer(r"firstByte: (\d+)\s+indexCount: (\d+)\s+topology: (\d+)\s+"
                                            r"baseVertex: (\d+)", body)):
        first, n, topology, base = (int(v) for v in m.groups())
        if topology != 0:
            raise VendorError(f"{path}: submesh {submesh} is not triangles")
        values = struct.unpack_from(f"<{n}{'I' if wide else 'H'}", indices, first)
        triangles.extend((values[i] + base, values[i + 1] + base, values[i + 2] + base, submesh)
                         for i in range(0, n, 3))
    return vertices, triangles, [tuple(texcoords[i] for i in t[:3]) for t in triangles]


# --- the textures -----------------------------------------------------------------------------------------------

UNITY_FORMATS = {10: ("bcn", 1), 12: ("bcn", 3), 4: ("raw", "RGBA"), 3: ("raw", "RGB"), 5: ("raw", "ARGB")}


def texture(path):
    """(image, bottom_up): a project texture as a PIL image with the alpha Unity imports. A Unity Texture2D asset
    (.asset) is decoded as stored, its first row at v = 0. An image file is read top row first, and its alpha as its
    .meta's import says: alphaUsage 1 (from input) takes the file's alpha, which for a TIFF of 4 samples whose 4th is
    marked 'unspecified' (ExtraSamples 0, Megascans' Albedo_Opacity maps) is that 4th sample, read here as Unity
    reads it where Pillow would drop it as padding; alphaUsage 0 imports none; another import is refused."""
    from PIL import Image  # the pinned python's Pillow; only the review and the samples need it
    if path.endswith(".asset"):
        image = _unity_texture(path)
        image.load()
        return image, True
    with open(path + ".meta", encoding="utf-8") as f:
        usage = _field(f.read(), "alphaUsage")
    image = Image.open(path)
    if usage == "0":
        image = image.convert("RGB")
    elif usage != "1":
        raise VendorError(f"{path}: its import's alphaUsage {usage} is not read here (0 none, 1 from input)")
    elif (image.format == "TIFF" and image.mode == "RGB" and image.tag_v2.get(277) == 4
          and tuple(image.tag_v2.get(338, ())) == (0,)):
        image.tile = [t._replace(args=(t.args[0].replace("RGBX", "RGBA", 1),) + tuple(t.args[1:])) for t in image.tile]
        image._mode = "RGBA"
    image.load()
    return image, False


class Alpha:
    """A texture's alpha as a function of (u, v), wrapping (Unity's Repeat), nearest texel: `opaque(u, v)` is
    alpha >= the material's cutoff, or everywhere when it has none (it draws whole). The texture as Unity imports it
    (`texture`); a material that clips on a texture giving no alpha is refused, never taken as opaque."""

    def __init__(self, path, cutoff, scale=(1.0, 1.0), offset=(0.0, 0.0)):
        self.data = None
        self.scale, self.offset = scale, offset
        if cutoff is None:
            return
        image, self.bottom_up = texture(path)
        if "A" not in image.getbands():
            raise VendorError(f"{path}: its material clips at {cutoff}, but the texture gives no alpha as Unity "
                              "imports it")
        self.width, self.height = image.size
        self.data = image.getchannel("A").tobytes()
        self.threshold = int(math.ceil(cutoff * 255 - 1e-9))

    def opaque(self, u, v):
        if self.data is None:
            return True
        u = (u * self.scale[0] + self.offset[0]) % 1.0
        v = (v * self.scale[1] + self.offset[1]) % 1.0
        x = min(int(u * self.width), self.width - 1)
        y = min(int(v * self.height), self.height - 1)
        row = y if self.bottom_up else self.height - 1 - y
        return self.data[row * self.width + x] >= self.threshold


def _unity_texture(path):
    """The first mip of a Unity Texture2D YAML asset as a PIL image (DXT1, DXT5, RGB24, RGBA32, ARGB32)."""
    from PIL import Image
    docs = _docs(path)
    body = next(b for kind, b in docs.values() if kind == "28")
    width, height = int(_field(body, "m_Width")), int(_field(body, "m_Height"))
    fmt = int(_field(body, "m_TextureFormat"))
    if fmt not in UNITY_FORMATS:
        raise VendorError(f"{path}: texture format {fmt} is not read here")
    data = bytes.fromhex(_field(body, "_typelessdata"))
    kind, arg = UNITY_FORMATS[fmt]
    if kind == "bcn":
        size = ((width + 3) // 4) * ((height + 3) // 4) * (8 if arg == 1 else 16)
        return Image.frombytes("RGBA", (width, height), data[:size], "bcn", arg)
    mode = arg
    size = width * height * len(mode)
    image = Image.frombytes("RGBA" if mode == "ARGB" else mode, (width, height), data[:size])
    if mode == "ARGB":
        a, r, g, b = image.split()
        image = Image.merge("RGBA", (r, g, b, a))
    return image


def material_maps(path, shader_source=None):
    """A material's base map guid, its tiling and offset, and its alpha cutoff (None when it draws whole).
    `shader_source(guid)` gives a project shader's text, or None for one outside the project (URP's): a project
    shader clips at _Cutoff when its passes call clip() on the base map's alpha against it, whatever the material's
    _AlphaClip says (the friend's leaf shader always does, the bark shader never); URP's Lit clips when _AlphaClip
    is 1."""
    with open(path, encoding="utf-8") as f:
        text = f.read()
    m = re.search(r"- _BaseMap:\s+m_Texture: \{fileID: -?\d+(?:, guid: (\w+))?[^}]*\}\s+m_Scale: \{([^}]*)\}\s+"
                  r"m_Offset: \{([^}]*)\}", text)
    clip = re.search(r"- _AlphaClip: ([\d.]+)", text)
    cutoff = re.search(r"- _Cutoff: ([\d.]+)", text)
    shader = re.search(r"m_Shader: \{fileID: -?\d+, guid: (\w+)", text)
    source = shader_source(shader.group(1)) if shader and shader_source else None
    if source is not None:
        clips = re.search(r"clip\([^;]*\.a\s*-\s*_Cutoff\)", source) is not None
    else:
        clips = bool(clip) and float(clip.group(1)) > 0
    return {"base_map": m.group(1) if m else None,
            "scale": _vec("{" + m.group(2) + "}", "xy") if m else (1.0, 1.0),
            "offset": _vec("{" + m.group(3) + "}", "xy") if m else (0.0, 0.0),
            "cutoff": float(cutoff.group(1)) if cutoff and clips else None,
            "shader": shader.group(1) if shader else None}


# --- the check --------------------------------------------------------------------------------------------------

def renderer_bounds(vertices, matrix):
    """A renderer's bounds as Unity takes them: the mesh's own box, its eight corners through the matrix."""
    lo = [min(v[a] for v in vertices) for a in range(3)]
    hi = [max(v[a] for v in vertices) for a in range(3)]
    corners = [apply(matrix, (x, y, z)) for x in (lo[0], hi[0]) for y in (lo[1], hi[1]) for z in (lo[2], hi[2])]
    return [min(c[a] for c in corners) for a in range(3)], [max(c[a] for c in corners) for a in range(3)]


def check_group(group, boxes, where):
    """Problems when the boxes of every renderer of the group (renderer_bounds) disagree with the LOD group's
    reference point and size."""
    lo = [min(b[0][a] for b in boxes) for a in range(3)]
    hi = [max(b[1][a] for b in boxes) for a in range(3)]
    centre = [(lo[a] + hi[a]) / 2 for a in range(3)]
    size = max(hi[a] - lo[a] for a in range(3))
    problems = []
    if any(abs(centre[a] - group["reference"][a]) > GROUP_TOLERANCE_M for a in range(3)):
        problems.append(f"{where}: the meshes' centre {[round(c, 4) for c in centre]} is not the LOD group's "
                        f"{list(group['reference'])}")
    if abs(size - group["size"]) > GROUP_TOLERANCE_M:
        problems.append(f"{where}: the meshes' size {size:.4f} is not the LOD group's {group['size']}")
    return problems
