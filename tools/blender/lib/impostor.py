"""The baked impostor (p1-v01-locust, design decision 3; asset-pipeline "Baked impostor LOD"; AST-08, AST-09): the
far LOD of a variant whose params set `impostor: true` and `lod.impostor_switch_m`, a camera-facing card that reads
an albedo atlas and a normal atlas of the LOD0 mesh rendered in headless Blender with the catalog's materials. The
importer builds the card as the last LOD before the cull.

Views (assumed in design 3: belts are seen near level or from a little above): VIEWS orthographic views from
ELEVATION_DEG above the horizon, view k from the azimuth a = k * 360 / VIEWS degrees (from +Z towards +X, asset
space), in cell k of a COLUMNS x ROWS grid over each SIZE_PX square atlas, left to right from the top row. View k's
image axes are right = (-cos a, 0, sin a) and up = (-sin e sin a, cos e, -sin e cos a), and the camera lies towards
(cos e sin a, sin e, cos e cos a): the image is what a camera there sees, unmirrored. Every view has the same scale,
the largest that keeps every view of the mesh PAD_PX inside its cell, with the asset's vertical axis through the
origin at the cell's horizontal centre.

The card: the shader turns an upright quad about that axis to face the camera (or, for shadows, the light) and maps
the nearest view onto it. The card's height is the image's up axis over cos e, so a viewer ELEVATION_DEG above the
horizon sees each view exactly, and a level one sees a point in front of the axis lowered by its depth times tan e
(a quarter of the crown's radius at most). Its normal, per pixel, is the normal atlas's: the visible side of the
surface under the pixel as its view saw it, with the slot's normal map applied as URP/Lit applies it (a back face's
whole frame turned to the viewer, as the leaf shader lights a leaf), in that view's axes (right, up, towards the
camera) mapped into 0-1, which the shader turns into object space with the same view's axes. So every leaf, limb and
trunk takes the sun at its own angle, from any sun, and its own relief takes the highlights and the sky's grazing
reflection as LOD0's does (the geometric normal alone left the card's specular about 40 % under the leaves', task
1.2g); the texels no surface covers are filled from the covered ones (push_pull), so neither the filter at the
crown's edge nor a mip mixes in a normal that no surface has. One albedo card with one normal for every sun left a
sunlit crown 8-18 % dark at 50 m (measured in task 1.2, design 3).

The renders are flat: each slot's base map, as the importer picks it (a groove, paint or separate-opacity recipe's
derived _base map, else the set's diffuse), times its tint, both linear as Unity takes them, clipped at the recipe's
cutoff and emitted, so the albedo atlas holds albedo and the gallery's light shades the card (design 3); the normal
render emits the mapped normal through the Raw view transform, so its PNG holds the values, linear, each texel's the
mean of the surfaces under it (straight alpha), and the atlas is written from it, filled, as 8-bit RGB. One more
render of the same views measures the crown's occlusion, the luminance of the base maps times their recipes' derived
occlusion (the mask's G, which URP/Lit applies to ambient light only) over that of the albedo atlas, which the card's
material applies to its ambient light, since one albedo card cannot carry it per pixel. Cycles on the CPU with fixed
samples and seed, no denoiser, the Standard view transform for albedo and no dither, so a render repeats; each PNG
keeps only its critical chunks. The metadata records the hash of everything the render reads (`inputs_sha256`) and
of each atlas (`atlas_sha256`, `normals_sha256`): atlases whose hashes all match are up to date and are not rendered
again (their measure stands), so an unchanged rebuild writes no byte, and a hand-edited atlas is rendered over."""
import hashlib
import json
import math
import os
import struct

VIEWS = 8
COLUMNS, ROWS = 4, 2
SIZE_PX = 1024              # the spec's 1k, both atlases
CELL_PX = (SIZE_PX // COLUMNS, SIZE_PX // ROWS)
ELEVATION_DEG = 15.0        # design 3 (assumed)
PAD_PX = 8                  # an empty margin round every view: a texel still at mip 3, where a card switched in at
                            # 24 px of crown samples, so no cell bleeds into the next (assumed: the bleed past it is
                            # a texel at 400 m and more)
FACING = (0.5, 0.5, 1.0)    # a normal atlas cell no surface covers: the view's own direction, (0, 0, 1) in 0-1
LUMA = (0.2126, 0.7152, 0.0722)          # linear Rec. 709 luminance
SAMPLES = 32               # Cycles samples per pixel: anti-aliasing and the texture average under a pixel
TRANSPARENT_BOUNCES = 256   # alpha-clipped cards a ray may pass through; a dense clump stacks dozens
FORMULA = "impostor 3: flat albedo x tint and the visible side's mapped normal, Cycles CPU"  # change when the render
                            # changes, so every atlas is redone
NO_ATLAS = "0" * 64         # an atlas's sha256 until it is rendered or found up to date
UV_MAP = "UVMap"            # the laid-out mesh's one UV layer, which the normal maps' tangent frames follow
MATERIALS = os.path.join("unity", "Assets", "_Project", "Art", "Materials")
RECIPES = os.path.join("assets-src", "materials")
MANIFEST = os.path.join("tools", "assets", "manifest.json")
LFS_POINTER = b"version https://git-lfs"


def switch_distance(params):
    """The impostor's switch distance when the params ask for one, else None."""
    if not params.get("impostor"):
        return None
    switch = params.get("lod", {}).get("impostor_switch_m")
    if isinstance(switch, bool) or not isinstance(switch, (int, float)):
        raise ValueError("'impostor' is true but no 'lod.impostor_switch_m' gives its switch distance")
    return float(switch)


def basis(k):
    """View k's image right and up axes and the direction towards its camera, unit vectors in asset space."""
    a, e = math.radians(360.0 * k / VIEWS), math.radians(ELEVATION_DEG)
    return ((-math.cos(a), 0.0, math.sin(a)),
            (-math.sin(e) * math.sin(a), math.cos(e), -math.sin(e) * math.cos(a)),
            (math.cos(e) * math.sin(a), math.sin(e), math.cos(e) * math.cos(a)))


def frame(mesh):
    """The card of the LOD0 `mesh` (module docstring): {"width_m", "height_m", "bottom_m"} in the card's own upright
    frame."""
    views = [[(_dot(p, right), _dot(p, up)) for p in mesh.vertices] for right, up, _ in map(basis, range(VIEWS))]
    half = max(max(abs(x) for view in views for x, _ in view), 1e-3)
    lo = min(y for view in views for _, y in view)
    hi = max(max(y for view in views for _, y in view), lo + 1e-3)
    scale = min((CELL_PX[0] - 2 * PAD_PX) / (2 * half), (CELL_PX[1] - 2 * PAD_PX) / (hi - lo))  # px per metre
    cos_e = math.cos(math.radians(ELEVATION_DEG))
    return {"width_m": CELL_PX[0] / scale, "height_m": CELL_PX[1] / scale / cos_e,
            "bottom_m": (lo - PAD_PX / scale) / cos_e}


def slot_materials(root, visual_by_slot, slots):
    """What the render reads per slot, as the importer builds the material (CatalogImporter.EnsureMaterial): the
    visual name, its recipe, the base map's path, the derived mask's and the set's normal map's (None without one),
    the tile size, the tint linear as Unity takes it, the tint's alpha and the alpha cutoff (None for an opaque
    recipe). Raises ValueError naming a missing recipe, set or map."""
    with open(os.path.join(root, MANIFEST), encoding="utf-8") as f:
        manifest = json.load(f)
    out = {}
    for slot in sorted(slots):
        visual = visual_by_slot[slot]
        path = os.path.join(root, RECIPES, visual + ".json")
        with open(path, encoding="utf-8") as f:
            recipe = json.load(f)
        painted, clipped = "paint" in recipe, "alpha_clip" in recipe
        maps = {} if painted else _set_maps(root, manifest, recipe["texture_set"], visual)
        if painted or "groove" in recipe or (clipped and "opacity" in maps):
            texture = os.path.join(root, MATERIALS, f"{visual}_base.png")
        elif "diffuse" in maps:
            texture = maps["diffuse"]
        else:
            raise ValueError(f"{visual}: texture set {recipe['texture_set']} has no diffuse map")
        _require_file(texture, visual)
        mask = os.path.join(root, MATERIALS, f"{visual}_mask.png") if "mask" in recipe else None
        normal = maps.get("normal")  # a painted recipe has none
        for path in (mask, normal):
            if path:
                _require_file(path, visual)
        tint = recipe["tint"]
        out[slot] = {"visual": visual, "recipe": recipe, "texture": texture, "mask": mask, "normal": normal,
                     "tile_m": list(recipe["tile_size_m"]), "tint": [gamma_to_linear(c) for c in tint[:3]],
                     "alpha": tint[3], "cutoff": recipe["alpha_clip"] if clipped else None}
    return out


def gamma_to_linear(value):
    """A material colour component as Unity's linear colour space takes it (Mathf.GammaToLinearSpace): the sRGB curve
    below 1, a 2.2 power from 1 up (the tints above 1 that the bark recipes use)."""
    if value <= 0.04045:
        return value / 12.92
    if value < 1.0:
        return ((value + 0.055) / 1.055) ** 2.4
    return value ** 2.2


def lod(variant, switch, mesh, materials):
    """The metadata's impostor LOD (lib.meta) for the LOD0 `mesh`: the card mesh's name and triangles, the switch
    distance, and the record of the two atlases, their views and the card, with the inputs' hash and NO_ATLAS for the
    atlases' own until they are rendered or found up to date."""
    record = {"atlas": f"{variant}_impostor.png", "normals": f"{variant}_impostor_normal.png",
              "size_px": [SIZE_PX, SIZE_PX], "views": VIEWS, "columns": COLUMNS, "rows": ROWS,
              "elevation_deg": ELEVATION_DEG, "card": frame(mesh)}
    record["inputs_sha256"] = inputs_sha256(mesh, record, materials)
    record["atlas_sha256"] = record["normals_sha256"] = NO_ATLAS
    record["occlusion"] = 1.0  # the render measures it
    return {"mesh": f"{variant}_impostor", "tris": 2, "switch_distance_m": float(switch), "impostor": record}


def inputs_sha256(mesh, record, materials):
    """SHA-256 of everything the render reads: the formula and the render's constants, the card, the LOD0 mesh
    (vertices, faces, UVs and slots, to the metadata's micrometre), and per slot its recipe, the bytes of its base map,
    mask and normal map, and whether it is shaded smooth."""
    digest = hashlib.sha256()

    def put(value):
        digest.update(json.dumps(value, sort_keys=True, separators=(",", ":")).encode("utf-8") + b"\n")

    put({"formula": FORMULA, "samples": SAMPLES, "transparent_bounces": TRANSPARENT_BOUNCES, "pad_px": PAD_PX, "keys": {k: record[k] for k in ("size_px", "views", "columns", "rows", "elevation_deg", "card")}})
    put([[round(c, 6) for c in v] for v in mesh.vertices])
    put([[indices, [[round(c, 6) for c in uv] for uv in uvs], slot] for indices, _, uvs, slot in mesh.faces])
    for slot in sorted(materials):
        m = materials[slot]
        put({"slot": slot, "visual": m["visual"], "recipe": m["recipe"], "smooth": slot in mesh.smooth})
        for path in (m["texture"], m.get("mask"), m.get("normal")):
            digest.update((file_sha256(path) if path else "-").encode("ascii"))
    return digest.hexdigest()


def file_sha256(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def recorded(meta_path, atlas_path, normals_path, inputs):
    """The impostor record the variant's current metadata holds, when it records these `inputs` and both atlases on
    disk still have their hashes; else None, and the atlases are rendered."""
    if not all(os.path.isfile(p) for p in (meta_path, atlas_path, normals_path)):
        return None
    with open(meta_path, encoding="utf-8") as f:
        lods = json.load(f).get("lods", [])
    old = next((l["impostor"] for l in lods if isinstance(l, dict) and "impostor" in l), None)
    if not old or old.get("inputs_sha256") != inputs:
        return None
    if file_sha256(atlas_path) != old.get("atlas_sha256") or file_sha256(normals_path) != old.get("normals_sha256"):
        return None
    return old


def layout(mesh, record, materials):
    """The VIEWS views of `mesh` laid side by side in the atlas plane, for one orthographic render: (vertices,
    faces, per-face UVs in tiles of each slot's base map, per-face slots, the atlas's side in metres). A vertex is
    (x right, y up, z towards the camera), metres, the atlas's lower-left corner at the origin. Every face is wound
    counter-clockwise about its outward normal there, as the FBX is (lib.export), so Blender's front face is Unity's
    and a normal map tilts the way it does in Unity (each view is view 0 turned about the vertical axis, so one
    winding holds for all)."""
    card = record["card"]
    scale = CELL_PX[0] / card["width_m"]
    cell_w, cell_h = card["width_m"], CELL_PX[1] / scale
    floor = card["bottom_m"] * math.cos(math.radians(ELEVATION_DEG))
    vertices = []
    for k in range(VIEWS):
        right, up, toward = basis(k)
        x0 = (k % COLUMNS + 0.5) * cell_w
        y0 = (ROWS - 1 - k // COLUMNS) * cell_h - floor
        vertices += [(x0 + _dot(p, right), y0 + _dot(p, up), _dot(p, toward)) for p in mesh.vertices]
    n = len(mesh.vertices)
    right, up, toward = basis(0)
    wound = []
    for indices, outward, face_uvs, slot in mesh.faces:
        if _dot(_newell(vertices, indices), (_dot(outward, right), _dot(outward, up), _dot(outward, toward))) < 0:
            indices, face_uvs = indices[::-1], face_uvs[::-1]
        wound.append((indices, face_uvs, slot))
    faces, uvs, slots = [], [], []
    for k in range(VIEWS):
        for indices, face_uvs, slot in wound:
            tile = materials[slot]["tile_m"]
            faces.append([i + k * n for i in indices])
            uvs.append([(u / tile[0], v / tile[1]) for u, v in face_uvs])
            slots.append(slot)
    return vertices, faces, uvs, slots, COLUMNS * cell_w


def render(mesh, record, materials, path, normals_path):
    """Renders the albedo atlas to `path` and the normal atlas to `normals_path` in this Blender (module docstring),
    replacing the scene, and returns what one more render of the same views measures: the crown's occlusion."""
    import bpy
    vertices, faces, uvs, slots, side = layout(mesh, record, materials)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    _settings(scene)
    me = bpy.data.meshes.new("impostor")
    me.from_pydata(vertices, [], faces)
    if me.validate(verbose=True):
        raise RuntimeError("impostor: Blender had to correct the laid-out mesh")
    if mesh.smooth:  # shaded as the FBX is
        me.polygons.foreach_set("use_smooth", [slot in mesh.smooth for slot in slots])
    order = sorted(set(slots))
    for slot in order:
        me.materials.append(_material(slot, materials[slot], "albedo"))
    layer = me.uv_layers.new(name=UV_MAP)
    for poly, face_uvs, slot in zip(me.polygons, uvs, slots):
        poly.material_index = order.index(slot)
        for loop_index, uv in zip(poly.loop_indices, face_uvs):
            layer.data[loop_index].uv = uv
    scene.collection.objects.link(bpy.data.objects.new("impostor", me))
    near, far = max(v[2] for v in vertices) + 1.0, min(v[2] for v in vertices) - 1.0
    camera = bpy.data.cameras.new("atlas")
    camera.type, camera.ortho_scale, camera.clip_start, camera.clip_end = 'ORTHO', side, 0.01, near - far
    camera_object = bpy.data.objects.new("atlas", camera)
    camera_object.location = (side / 2, side / 2, near)
    scene.collection.objects.link(camera_object)
    scene.camera = camera_object
    _shoot(scene, path, 6)
    occlusion, scratch = 1.0, path[:-len(".png")] + ".measure.png"
    try:
        if any(materials[slot]["mask"] for slot in order):
            for i, slot in enumerate(order):
                me.materials[i] = _material(slot, materials[slot], "occlusion")
            _shoot(scene, scratch, 6)
            occlusion = _luminance(_pixels(scratch)) / _luminance(_pixels(path))
    finally:
        if os.path.exists(scratch):
            os.remove(scratch)
    for i, slot in enumerate(order):
        me.materials[i] = _material(slot, materials[slot], "normal")
    scene.view_settings.view_transform = 'Raw'  # the PNG holds the emitted values: the normal, linear
    try:
        _shoot(scene, scratch, 6)
        _write_rgb(normals_path, _filled(_pixels(scratch)))
    finally:
        if os.path.exists(scratch):
            os.remove(scratch)
    return occlusion


def _shoot(scene, path, colour_type):
    """Renders the scene to `path` and checks it came out SIZE_PX square, 8-bit, of the PNG colour type (6 RGBA,
    2 RGB)."""
    import bpy
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    strip_png(path)
    size = png_header(path)
    if size != (SIZE_PX, SIZE_PX, 8, colour_type):
        raise RuntimeError(f"impostor: {path} came out {size}, expected {SIZE_PX}x{SIZE_PX} 8-bit of colour type "
                           f"{colour_type}")


def _pixels(path):
    """A rendered PNG's pixels as numpy rows (r, g, b, a): its encoded values over 255, straight alpha."""
    import bpy
    import numpy as np
    image = bpy.data.images.load(path)
    try:
        pixels = np.empty(image.size[0] * image.size[1] * 4, dtype=np.float32)
        image.pixels.foreach_get(pixels)
    finally:
        bpy.data.images.remove(image)
    return pixels.reshape(-1, 4).astype(np.float64)


def _linear(encoded):
    import numpy as np
    return np.where(encoded <= 0.04045, encoded / 12.92, ((encoded + 0.055) / 1.055) ** 2.4)


def _luminance(pixels):
    """The pixels' coverage-weighted linear luminance, summed."""
    return float((pixels[:, 3] * (_linear(pixels[:, :3]) @ LUMA)).sum())


def _settings(scene):
    scene.render.engine = 'CYCLES'
    cycles = scene.cycles
    cycles.device = 'CPU'
    cycles.samples = SAMPLES
    cycles.use_adaptive_sampling = False
    cycles.use_denoising = False
    cycles.seed = 0
    cycles.max_bounces = 0
    cycles.transparent_max_bounces = TRANSPARENT_BOUNCES
    cycles.pixel_filter_type = 'BLACKMAN_HARRIS'
    cycles.filter_width = 1.5
    render = scene.render
    render.film_transparent = True
    render.resolution_x = render.resolution_y = SIZE_PX
    render.resolution_percentage = 100
    render.dither_intensity = 0.0
    render.use_file_extension = False
    for name in dir(render):
        if name.startswith("use_stamp"):
            setattr(render, name, False)
    scene.display_settings.display_device = 'sRGB'
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.look = 'None'
    scene.view_settings.exposure = 0.0
    scene.view_settings.gamma = 1.0
    image = render.image_settings
    image.file_format, image.color_mode, image.color_depth, image.compression = 'PNG', 'RGBA', '8', 15


def _filled(pixels):
    """The normal render's rows (r, g, b, a, bottom row first, straight alpha) as SIZE_PX x SIZE_PX x 3 bytes, top row
    first, every texel no surface covers filled from its cell's covered ones (`push_pull`), so neither the bilinear
    filter nor a mip ever mixes in a normal no surface has."""
    import numpy as np
    image = pixels.reshape(SIZE_PX, SIZE_PX, 4)[::-1]
    out = np.empty((SIZE_PX, SIZE_PX, 3))
    for row in range(ROWS):
        for column in range(COLUMNS):
            cell = (slice(row * CELL_PX[1], (row + 1) * CELL_PX[1]), slice(column * CELL_PX[0], (column + 1) * CELL_PX[0]))
            out[cell] = push_pull(image[cell][..., :3], image[cell][..., 3] > 0)
    return np.clip(np.round(out * 255.0), 0, 255).astype(np.uint8)


def push_pull(values, known):
    """`values` (h x w x c, numpy) with every texel where `known` is False filled: from the means of the known ones
    over 2 x 2 blocks, level by level up to one texel, then back down, so a hole takes the mean of the nearest known
    texels at the finest level that has any; FACING when none is known."""
    import numpy as np
    h, w = known.shape
    if known.all():
        return values
    if not known.any():
        return np.broadcast_to(np.array(FACING[:values.shape[2]], dtype=float), values.shape).copy()
    if h % 2 or w % 2:
        mean = values[known].mean(axis=0)
        return np.where(known[..., None], values, mean)
    weight = known.reshape(h // 2, 2, w // 2, 2).sum(axis=(1, 3))
    total = (values * known[..., None]).reshape(h // 2, 2, w // 2, 2, -1).sum(axis=(1, 3))
    coarse = push_pull(total / np.maximum(weight, 1)[..., None], weight > 0)
    return np.where(known[..., None], values, coarse.repeat(2, axis=0).repeat(2, axis=1))


def _write_rgb(path, rgb):
    """An 8-bit RGB PNG of `rgb` (h x w x 3 bytes, top row first): IHDR, one IDAT of unfiltered rows at zlib level 9,
    IEND, so the same texels always make the same bytes."""
    import zlib
    h, w, _ = rgb.shape
    raw = b"".join(b"\x00" + rgb[y].tobytes() for y in range(h))

    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))

    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
                + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def _material(slot, m, mode):
    """Emission of the base map times the tint ("albedo"), times the mask's occlusion as well ("occlusion"), or of the
    visible side's normal mapped into 0-1 ("normal"): the slot's normal map applied in the UVs' tangent frame, as
    URP/Lit applies it (Cycles' tangent-space normal map node: MikkTSpace tangents, B = sign x cross(N, T), and a
    back face's whole frame turned, as the leaf shader turns it), else the shading normal; a clipped recipe is
    transparent where its alpha falls under the cutoff."""
    import bpy
    material = bpy.data.materials.new(f"{slot}_{mode}")
    nodes, links = material.node_tree.nodes, material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    texture = _texture(nodes, m["texture"], 'sRGB')
    tint = nodes.new("ShaderNodeVectorMath")
    tint.operation = 'MULTIPLY'
    tint.inputs[1].default_value = m["tint"]
    links.new(texture.outputs["Color"], tint.inputs[0])
    colour = tint.outputs["Vector"]
    if mode == "occlusion" and m["mask"]:
        channels = nodes.new("ShaderNodeSeparateColor")
        links.new(_texture(nodes, m["mask"], 'Non-Color').outputs["Color"], channels.inputs["Color"])
        occlusion = nodes.new("ShaderNodeVectorMath")
        occlusion.operation = 'SCALE'
        links.new(colour, occlusion.inputs[0])
        links.new(channels.outputs["Green"], occlusion.inputs["Scale"])
        colour = occlusion.outputs["Vector"]
    elif mode == "normal":  # Cycles turns a back face's normal to the viewer, as the leaf shader does
        mapped = nodes.new("ShaderNodeVectorMath")
        mapped.operation = 'MULTIPLY_ADD'
        mapped.inputs[1].default_value = (0.5, 0.5, 0.5)
        mapped.inputs[2].default_value = (0.5, 0.5, 0.5)
        if m["normal"]:
            normal_map = nodes.new("ShaderNodeNormalMap")
            normal_map.space, normal_map.uv_map = 'TANGENT', UV_MAP
            links.new(_texture(nodes, m["normal"], 'Non-Color').outputs["Color"], normal_map.inputs["Color"])
            links.new(normal_map.outputs["Normal"], mapped.inputs[0])
        else:
            links.new(nodes.new("ShaderNodeNewGeometry").outputs["Normal"], mapped.inputs[0])
        colour = mapped.outputs["Vector"]
    emission = nodes.new("ShaderNodeEmission")
    emission.inputs["Strength"].default_value = 1.0
    links.new(colour, emission.inputs["Color"])
    if m["cutoff"] is None:
        links.new(emission.outputs["Emission"], output.inputs["Surface"])
        return material
    alpha = nodes.new("ShaderNodeMath")
    alpha.operation = 'MULTIPLY'
    alpha.inputs[1].default_value = m["alpha"]
    links.new(texture.outputs["Alpha"], alpha.inputs[0])
    kept = nodes.new("ShaderNodeMath")
    kept.operation = 'GREATER_THAN'
    kept.inputs[1].default_value = m["cutoff"]
    links.new(alpha.outputs["Value"], kept.inputs[0])
    mix = nodes.new("ShaderNodeMixShader")
    links.new(kept.outputs["Value"], mix.inputs["Fac"])
    links.new(nodes.new("ShaderNodeBsdfTransparent").outputs["BSDF"], mix.inputs[1])
    links.new(emission.outputs["Emission"], mix.inputs[2])
    links.new(mix.outputs["Shader"], output.inputs["Surface"])
    return material


def _texture(nodes, path, colorspace):
    import bpy
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = bpy.data.images.load(path, check_existing=True)
    texture.image.colorspace_settings.name = colorspace
    texture.image.alpha_mode = 'STRAIGHT'
    texture.interpolation, texture.extension = 'Linear', 'REPEAT'
    return texture


def strip_png(path):
    """Rewrites the PNG with its critical chunks only (IHDR, PLTE, IDAT, IEND), so no date or tag Blender writes
    can make two renders of the same pixels differ."""
    with open(path, "rb") as f:
        data = f.read()
    kept, pos = [data[:8]], 8
    while pos < len(data):
        length, kind = struct.unpack(">I4s", data[pos:pos + 8])
        if kind[:1].isupper():
            kept.append(data[pos:pos + 12 + length])
        pos += 12 + length
    with open(path, "wb") as f:
        f.write(b"".join(kept))


def png_header(path):
    """(width, height, bit depth, colour type) from the PNG's IHDR; colour type 6 is RGBA, 2 RGB."""
    with open(path, "rb") as f:
        head = f.read(26)
    width, height, depth, colour = struct.unpack(">IIBB", head[16:26])
    return width, height, depth, colour


def _set_maps(root, manifest, texture_set, visual):
    for e in manifest["entries"]:
        if f"{e['source']}/{e['id']}" == texture_set:
            return {f["map"]: os.path.join(root, *e["destination"].split("/"), f["name"]) for f in e["files"]}
    raise ValueError(f"{visual}: texture set {texture_set} is not in {MANIFEST}")


def _require_file(path, visual):
    if not os.path.isfile(path):
        raise ValueError(f"{visual}: {path} is missing (run python tools/assets/derive.py --all, or fetch.py)")
    with open(path, "rb") as f:
        if f.read(len(LFS_POINTER)) == LFS_POINTER:
            raise ValueError(f"{visual}: {path} is a Git LFS pointer; run git lfs checkout")


def _dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def _newell(vertices, indices):
    """The normal of a counter-clockwise loop (Newell's method), unnormalised, as lib.export winds the FBX."""
    n = [0.0, 0.0, 0.0]
    for i, j in zip(indices, indices[1:] + indices[:1]):
        a, b = vertices[i], vertices[j]
        n[0] += (a[1] - b[1]) * (a[2] + b[2])
        n[1] += (a[2] - b[2]) * (a[0] + b[0])
        n[2] += (a[0] - b[0]) * (a[1] + b[1])
    return n
