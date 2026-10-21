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
(a quarter of the crown's radius at most). Its normals, per pixel, are the normal atlas's: the visible sides of the
surfaces under the texel as its view saw them, each its geometric shading normal (a back face's turned to the viewer,
as the leaf shader turns a leaf's), in that view's axes (right, up, towards the camera) mapped into 0-1. A texel holds
their mean, unnormalised: its direction is where the surfaces face, its length R (1 for one flat surface, less the
more they differ) their spread, which the shader lights as a distribution of normals and not as their mean (task
1.2n: lit at its mean direction, V01_M's texels took 7 % less of the review's sun than their surfaces lit point by
point and 67 % less of the sky's grazing reflection, measured). The normals are the geometry's, not the slots' normal
maps: wherever the card is shown the mesh LODs read a leaf's or a bark's normal map at a mip far coarser than its
relief, which flattens it to the surface's own normal (task 1.3: V01_M's LOD0 at 50 m moved 0.1 % with the leaves'
normal maps off; task 1.2n: at 230 m its LOD0 and LOD2 reflect the sky at grazing angles as the geometric normals
do, 0.075 and 0.070 of Pow4(1 - n.v) against their 0.071, where the mapped normals' relief gives 0.177, measured; the
maps' relief was most of a texel's spread, R 0.78 against the geometry's 0.93). Its alpha is the occlusion of the
surface under the texel, its recipe's derived occlusion (the mask's G, which
URP/Lit applies to the ambient light and its reflections only; 1 for a recipe without a mask), which the shader sets
as the surface's occlusion, so a leaf's ambient and sky reflection are occluded as the mesh LODs' leaf is and a limb's
as its bark is (one crown-wide occlusion, the albedo-weighted mean, gave the leaves the bark's 0.56 in part and the bark
the leaves' 0.97: per texel V01_M's card gains 1.0 point against LOD2 at 190 m by day, task 1.2m, measured). The
smoothness atlas, one grey channel, holds the smoothness of the surfaces under each texel, its recipe's as URP/Lit
reads it (the mask's A, as `_MetallicGlossMap` with `_Smoothness` at 1, else the recipe's constant), which the shader
sets as the surface's smoothness, so a limb's texel takes its bark's and a leaf's the leaves' (one card-wide 0.2, the
leaves', gave V01_L's wood texels twice its bark's mean of 0.096: most of the card's excess overcast, task 1.2n,
measured; task 1.6b). The texels no surface covers are filled from the covered ones (push_pull), so neither the filter
at the crown's edge nor a mip mixes in a normal, an occlusion or a smoothness that no surface has. One albedo card with
one normal for every sun left a sunlit crown 8-18 % dark at 50 m (measured in task 1.2, design 3).

The renders are flat: each slot's base map, as the importer picks it (a groove, paint or separate-opacity recipe's
derived _base map, else the set's diffuse), times its tint, both linear as Unity takes them, clipped at the recipe's
cutoff and emitted, so the albedo atlas holds albedo and the gallery's light shades the card (design 3); the normal
render emits the shading normal and the surface render the occlusion in R and the smoothness in G, both clipped the
same way and through the Raw view transform, so their PNGs hold the values, linear, each texel's the mean of the
surfaces under it (straight alpha); the normal atlas is written from the normals and the occlusion, filled, as 8-bit
RGBA, and the smoothness atlas from the smoothness, filled, as 8-bit grey. Cycles on the CPU with fixed samples and
seed, no denoiser, the Standard view transform for albedo and no dither, so a render repeats; each PNG keeps only its
critical chunks. The metadata records the hash of everything the render reads (`inputs_sha256`) and of each atlas
(`atlas_sha256`, `normals_sha256`, `smoothness_sha256`): atlases whose hashes all match are up to date and are not
rendered again (their measure stands), so an unchanged rebuild writes no byte, and a hand-edited atlas is rendered
over."""
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
EMPTY = (0.5, 0.5, 1.0, 1.0)  # a normal atlas cell no surface covers: the view's own direction, (0, 0, 1) in 0-1,
                              # unoccluded
EMPTY_SMOOTHNESS = (0.0,)   # a smoothness atlas cell no surface covers: matte
SAMPLES = 32               # Cycles samples per pixel: anti-aliasing and the texture average under a pixel
TRANSPARENT_BOUNCES = 256   # alpha-clipped cards a ray may pass through; a dense clump stacks dozens
FORMULA = ("impostor 6: flat albedo x tint, the visible sides' geometric normals' mean, its length their spread, "
           "their occlusion in the normal atlas's alpha, and their smoothness in a grey atlas, Cycles CPU")  # change
                                                                       # when the render changes, so every atlas is
                                                                       # redone
NO_ATLAS = "0" * 64         # an atlas's sha256 until it is rendered or found up to date
UV_MAP = "UVMap"            # the laid-out mesh's one UV layer, which the base maps and masks read
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
    visual name, its recipe, the base map's path, the derived mask's (None without one), the tile size, the tint
    linear as Unity takes it, the tint's alpha, the alpha cutoff (None for an opaque recipe) and the recipe's constant
    smoothness (None with a mask, whose alpha carries it). Raises ValueError naming a missing recipe, set, map or
    smoothness."""
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
        if mask:
            _require_file(mask, visual)
        elif not isinstance(recipe.get("smoothness"), (int, float)):
            raise ValueError(f"{visual}: no mask and no constant smoothness (CatalogImporter.EnsureMaterial)")
        tint = recipe["tint"]
        out[slot] = {"visual": visual, "recipe": recipe, "texture": texture, "mask": mask,
                     "tile_m": list(recipe["tile_size_m"]), "tint": [gamma_to_linear(c) for c in tint[:3]],
                     "alpha": tint[3], "cutoff": recipe["alpha_clip"] if clipped else None,
                     "smoothness": None if mask else float(recipe["smoothness"])}
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
    distance, `thin_wire` false (every LOD entry carries it; the card draws with the impostor shader), and the record
    of the three atlases, their views and the card, with the inputs' hash and NO_ATLAS for the atlases' own until
    they are rendered or found up to date."""
    record = {"atlas": f"{variant}_impostor.png", "normals": f"{variant}_impostor_normal.png",
              "smoothness": f"{variant}_impostor_smoothness.png",
              "size_px": [SIZE_PX, SIZE_PX], "views": VIEWS, "columns": COLUMNS, "rows": ROWS,
              "elevation_deg": ELEVATION_DEG, "card": frame(mesh)}
    record["inputs_sha256"] = inputs_sha256(mesh, record, materials)
    record["atlas_sha256"] = record["normals_sha256"] = record["smoothness_sha256"] = NO_ATLAS
    return {"mesh": f"{variant}_impostor", "tris": 2, "switch_distance_m": float(switch), "thin_wire": False,
            "impostor": record}


def inputs_sha256(mesh, record, materials):
    """SHA-256 of everything the render reads: the formula and the render's constants, the card, the LOD0 mesh
    (vertices, faces, UVs and slots, to the metadata's micrometre), and per slot its recipe, the bytes of its base map
    and mask, and whether it is shaded smooth (its normals, and so their mean and spread)."""
    digest = hashlib.sha256()

    def put(value):
        digest.update(json.dumps(value, sort_keys=True, separators=(",", ":")).encode("utf-8") + b"\n")

    put({"formula": FORMULA, "samples": SAMPLES, "transparent_bounces": TRANSPARENT_BOUNCES, "pad_px": PAD_PX, "keys": {k: record[k] for k in ("size_px", "views", "columns", "rows", "elevation_deg", "card")}})
    put([[round(c, 6) for c in v] for v in mesh.vertices])
    put([[indices, [[round(c, 6) for c in uv] for uv in uvs], slot] for indices, _, uvs, slot in mesh.faces])
    for slot in sorted(materials):
        m = materials[slot]
        put({"slot": slot, "visual": m["visual"], "recipe": m["recipe"], "smooth": slot in mesh.smooth})
        for path in (m["texture"], m.get("mask")):
            digest.update((file_sha256(path) if path else "-").encode("ascii"))
    return digest.hexdigest()


def file_sha256(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def recorded(meta_path, atlas_path, normals_path, smoothness_path, inputs):
    """The impostor record the variant's current metadata holds, when it records these `inputs` and the three atlases
    on disk still have their hashes; else None, and the atlases are rendered."""
    atlases = {"atlas_sha256": atlas_path, "normals_sha256": normals_path, "smoothness_sha256": smoothness_path}
    if not all(os.path.isfile(p) for p in (meta_path, *atlases.values())):
        return None
    with open(meta_path, encoding="utf-8") as f:
        lods = json.load(f).get("lods", [])
    old = next((l["impostor"] for l in lods if isinstance(l, dict) and "impostor" in l), None)
    if not old or old.get("inputs_sha256") != inputs:
        return None
    if any(file_sha256(path) != old.get(key) for key, path in atlases.items()):
        return None
    return old


def layout(mesh, record, materials):
    """The VIEWS views of `mesh` laid side by side in the atlas plane, for one orthographic render: (vertices,
    faces, per-face UVs in tiles of each slot's base map, per-face slots, the atlas's side in metres). A vertex is
    (x right, y up, z towards the camera), metres, the atlas's lower-left corner at the origin. Every face is wound
    counter-clockwise about its outward normal there, as the FBX is (lib.export), so Blender's front face is Unity's
    and a smooth slot's vertex normals are the FBX's (each view is view 0 turned about the vertical axis, so one
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


def render(mesh, record, materials, path, normals_path, smoothness_path):
    """Renders the albedo atlas to `path`, the normal atlas, the occlusion in its alpha, to `normals_path` and the
    smoothness atlas to `smoothness_path` in this Blender (module docstring), replacing the scene."""
    import bpy
    import numpy as np
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
    scene.view_settings.view_transform = 'Raw'  # the PNGs hold the emitted values, linear: the surface, the normal
    scratch = path[:-len(".png")] + ".measure.png"
    try:
        for i, slot in enumerate(order):
            me.materials[i] = _material(slot, materials[slot], "surface")
        _shoot(scene, scratch, 6)
        surface = _pixels(scratch)  # R the occlusion, G the smoothness
        for i, slot in enumerate(order):
            me.materials[i] = _material(slot, materials[slot], "normal")
        _shoot(scene, scratch, 6)
        normals = _pixels(scratch)
    finally:
        if os.path.exists(scratch):
            os.remove(scratch)
    # the same views, cutoffs and samples: both renders cover the same texels, those of the normal render's alpha
    covered = normals[:, 3] > 0
    _write_png(normals_path, _filled(np.concatenate([normals[:, :3], surface[:, :1]], axis=1), covered, EMPTY))
    _write_png(smoothness_path, _filled(surface[:, 1:2], covered, EMPTY_SMOOTHNESS))


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


def _filled(values, covered, empty):
    """A render's rows of `values` (bottom row first, c channels in 0-1, straight alpha) as SIZE_PX x SIZE_PX x c
    bytes, top row first, every texel no surface covers (where `covered` is False) filled from its cell's covered ones
    (`push_pull`, `empty` in a cell with none), so neither the bilinear filter nor a mip ever mixes in a value no
    surface has."""
    import numpy as np
    channels = values.shape[1]
    image = values.reshape(SIZE_PX, SIZE_PX, channels)[::-1]
    known = covered.reshape(SIZE_PX, SIZE_PX)[::-1]
    out = np.empty((SIZE_PX, SIZE_PX, channels))
    for row in range(ROWS):
        for column in range(COLUMNS):
            cell = (slice(row * CELL_PX[1], (row + 1) * CELL_PX[1]), slice(column * CELL_PX[0], (column + 1) * CELL_PX[0]))
            out[cell] = push_pull(image[cell], known[cell], empty)
    return np.clip(np.round(out * 255.0), 0, 255).astype(np.uint8)


def push_pull(values, known, empty=EMPTY):
    """`values` (h x w x c, numpy) with every texel where `known` is False filled: from the means of the known ones
    over 2 x 2 blocks, level by level up to one texel, then back down, so a hole takes the mean of the nearest known
    texels at the finest level that has any; `empty` when none is known."""
    import numpy as np
    h, w = known.shape
    if known.all():
        return values
    if not known.any():
        return np.broadcast_to(np.array(empty[:values.shape[2]], dtype=float), values.shape).copy()
    if h % 2 or w % 2:
        mean = values[known].mean(axis=0)
        return np.where(known[..., None], values, mean)
    weight = known.reshape(h // 2, 2, w // 2, 2).sum(axis=(1, 3))
    total = (values * known[..., None]).reshape(h // 2, 2, w // 2, 2, -1).sum(axis=(1, 3))
    coarse = push_pull(total / np.maximum(weight, 1)[..., None], weight > 0, empty)
    return np.where(known[..., None], values, coarse.repeat(2, axis=0).repeat(2, axis=1))


def _write_png(path, pixels):
    """An 8-bit PNG of `pixels` (h x w x c bytes, top row first; c 4 for RGBA, 1 for grey): IHDR, one IDAT of
    unfiltered rows at zlib level 9, IEND, so the same texels always make the same bytes."""
    import zlib
    h, w, channels = pixels.shape
    colour_type = {4: 6, 1: 0}[channels]
    raw = b"".join(b"\x00" + pixels[y].tobytes() for y in range(h))

    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))

    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, colour_type, 0, 0, 0))
                + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def _material(slot, m, mode):
    """Emission of the base map times the tint ("albedo"), of the recipe's occlusion in R and its smoothness in G
    ("surface": the mask's G, as URP/Lit reads its occlusion map, else 1; the mask's A, as URP/Lit reads it with
    `_Smoothness` at 1, else the recipe's constant), or of the visible side's shading normal mapped into 0-1
    ("normal", no normal map: module docstring); a clipped recipe is transparent where its alpha falls under the
    cutoff."""
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
    if mode == "surface":
        surface = nodes.new("ShaderNodeCombineXYZ")
        surface.inputs["X"].default_value = 1.0
        surface.inputs["Y"].default_value = 0.0 if m["smoothness"] is None else m["smoothness"]
        if m["mask"]:
            mask = _texture(nodes, m["mask"], 'Non-Color')
            channels = nodes.new("ShaderNodeSeparateColor")
            links.new(mask.outputs["Color"], channels.inputs["Color"])
            links.new(channels.outputs["Green"], surface.inputs["X"])
            if m["smoothness"] is None:
                links.new(mask.outputs["Alpha"], surface.inputs["Y"])
        colour = surface.outputs["Vector"]
    elif mode == "normal":  # Cycles turns a back face's normal to the viewer, as the leaf shader does
        mapped = nodes.new("ShaderNodeVectorMath")
        mapped.operation = 'MULTIPLY_ADD'
        mapped.inputs[1].default_value = (0.5, 0.5, 0.5)
        mapped.inputs[2].default_value = (0.5, 0.5, 0.5)
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
