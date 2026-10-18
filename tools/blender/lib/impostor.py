"""The baked impostor (p1-v01-locust, design decision 3; asset-pipeline "Baked impostor LOD"; AST-08, AST-09): the
far LOD of a variant whose params set `impostor: true` and `lod.impostor_switch_m`, a camera-facing card that reads
an atlas of the LOD0 mesh rendered in headless Blender with the catalog's materials. The importer builds the card as
the last LOD before the cull.

Views (assumed in design 3: belts are seen near level or from a little above): VIEWS orthographic views from
ELEVATION_DEG above the horizon, view k from the azimuth a = k * 360 / VIEWS degrees (from +Z towards +X, asset
space), in cell k of a COLUMNS x ROWS grid over the SIZE_PX square atlas, left to right from the top row. View k's
image axes are right = (-cos a, 0, sin a) and up = (-sin e sin a, cos e, -sin e cos a), and the camera lies towards
(cos e sin a, sin e, cos e cos a): the image is what a camera there sees, unmirrored. Every view has the same scale,
the largest that keeps every view of the mesh PAD_PX inside its cell, with the asset's vertical axis through the
origin at the cell's horizontal centre.

The card: the shader turns an upright quad about that axis to face the camera (or, for shadows, the light) and maps
the nearest view onto it. The card's height is the image's up axis over cos e, so a viewer ELEVATION_DEG above the
horizon sees each view exactly, and a level one sees a point in front of the axis lowered by its depth times tan e
(a quarter of the crown's radius at most). Its normal, per pixel, is the crown's (card_normals): the normal of an
ellipsoid round the card slots' vertices (alpha-clipped recipes; the whole mesh without any), their centre and their
horizontal and vertical radii, on the side facing the camera (above and below the crown, where trunks and stems
stand, an upright cylinder's), leaning up by the crown's up share. An ellipsoid's visible normals spread as the
visible sides of leaves turned every way do (a projected-area-weighted hemisphere), so the card takes the sun as a
crown of leaves does from any side; a horizontal-only normal (the leaf cards' outward rule) left a side-lit crown a
third too dark (measured on V01 at 50 m).

The render is flat: each slot's base map, as the importer picks it (a groove, paint or separate-opacity recipe's
derived _base map, else the set's diffuse), times its tint, both linear as Unity takes them, clipped at the recipe's
cutoff and emitted, so the atlas holds albedo and the gallery's light shades the card (design 3). Two more renders of
the same views measure what the card's lighting needs, since one albedo card cannot carry it per pixel: the crown's
occlusion, the luminance of the base maps times their recipes' derived occlusion (the mask's G, which URP/Lit applies
to ambient light only) over that of the atlas, which the card's material applies to its ambient light; and from the
normals the views saw, the up share (up_share). Cycles on the CPU with fixed samples and seed, no denoiser, the
Standard view transform and no dither, so a render repeats; the PNG keeps only its critical chunks. The metadata
records the hash of everything the render reads (`inputs_sha256`) and of the atlas (`atlas_sha256`): an atlas whose
two hashes match is up to date and is not rendered again (its measures stand), so an unchanged rebuild writes no
byte, and a hand-edited atlas is rendered over."""
import hashlib
import json
import math
import os
import struct

VIEWS = 8
COLUMNS, ROWS = 4, 2
SIZE_PX = 1024              # the spec's 1k
CELL_PX = (SIZE_PX // COLUMNS, SIZE_PX // ROWS)
ELEVATION_DEG = 15.0        # design 3 (assumed)
PAD_PX = 8                  # an empty margin round every view: a texel still at mip 3, where a card switched in at
                            # 24 px of crown samples, so no cell bleeds into the next (assumed: the bleed past it is
                            # a texel at 400 m and more)
MAX_UP = 0.95               # the card's normal never lies flat
SUN_ELEVATIONS_DEG = (30.0, 45.0, 60.0)  # the suns the up share is matched under, at every view's azimuth (assumed:
                                         # the day's sun over the belts)
LUMA = (0.2126, 0.7152, 0.0722)          # linear Rec. 709 luminance
SAMPLES = 32               # Cycles samples per pixel: anti-aliasing and the texture average under a pixel
TRANSPARENT_BOUNCES = 256   # alpha-clipped cards a ray may pass through; a dense clump stacks dozens
FORMULA = "impostor 1: flat albedo x tint, Cycles CPU"  # change when the render changes, so every atlas is redone
NO_ATLAS = "0" * 64         # atlas_sha256 until the atlas is rendered or found up to date
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


def crown_slots(visual_by_slot, card_visuals):
    """The slots drawn as cards (catalog.card_visuals): the crown the card's normal follows."""
    return {slot for slot, visual in visual_by_slot.items() if visual in card_visuals}


def frame(mesh, crowned):
    """The card and the crown of the LOD0 `mesh` (module docstring): {"width_m", "height_m", "bottom_m"} in the card's
    own upright frame, and {"centre": [x, y, z], "radii_m": [horizontal, vertical]} of the vertices of the `crowned`
    slots (all vertices when no face has one): the middle of their box, the farthest of them from the vertical through
    it, half their height."""
    views = [[(_dot(p, right), _dot(p, up)) for p in mesh.vertices] for right, up, _ in map(basis, range(VIEWS))]
    half = max(max(abs(x) for view in views for x, _ in view), 1e-3)
    lo = min(y for view in views for _, y in view)
    hi = max(max(y for view in views for _, y in view), lo + 1e-3)
    scale = min((CELL_PX[0] - 2 * PAD_PX) / (2 * half), (CELL_PX[1] - 2 * PAD_PX) / (hi - lo))  # px per metre
    cos_e = math.cos(math.radians(ELEVATION_DEG))
    card = {"width_m": CELL_PX[0] / scale, "height_m": CELL_PX[1] / scale / cos_e,
            "bottom_m": (lo - PAD_PX / scale) / cos_e}
    picked = {i for indices, _, _, slot in mesh.faces if slot in crowned for i in indices}
    points = [mesh.vertices[i] for i in sorted(picked)] or mesh.vertices
    centre = [(min(p[a] for p in points) + max(p[a] for p in points)) / 2 for a in range(3)]
    radii = [max(max(math.hypot(p[0] - centre[0], p[2] - centre[2]) for p in points), 1e-3),
             max((max(p[1] for p in points) - min(p[1] for p in points)) / 2, 1e-3)]
    return card, {"centre": centre, "radii_m": radii}


def slot_materials(root, visual_by_slot, slots):
    """What the render reads per slot, as the importer builds the material (CatalogImporter.EnsureMaterial): the
    visual name, its recipe, the base map's path, the tile size, the tint linear as Unity takes it, the tint's alpha
    and the alpha cutoff (None for an opaque recipe). Raises ValueError naming a missing recipe, set or map."""
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
        tint = recipe["tint"]
        out[slot] = {"visual": visual, "recipe": recipe, "texture": texture, "mask": mask,
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


def lod(variant, switch, mesh, crowned, materials):
    """The metadata's impostor LOD (lib.meta) for the LOD0 `mesh`: the card mesh's name and triangles, the switch
    distance, and the record of the atlas, its views, the card and the crown, with the inputs' hash and NO_ATLAS for
    the atlas's own until it is rendered or found up to date."""
    card, crown = frame(mesh, crowned)
    record = {"atlas": f"{variant}_impostor.png", "size_px": [SIZE_PX, SIZE_PX], "views": VIEWS, "columns": COLUMNS,
              "rows": ROWS, "elevation_deg": ELEVATION_DEG, "card": card, "crown": crown}
    record["inputs_sha256"] = inputs_sha256(mesh, record, materials)
    record["atlas_sha256"] = NO_ATLAS
    crown["up"], crown["occlusion"] = 0.0, 1.0  # the render measures both
    return {"mesh": f"{variant}_impostor", "tris": 2, "switch_distance_m": float(switch), "impostor": record}


def inputs_sha256(mesh, record, materials):
    """SHA-256 of everything the render reads: the formula and the render's constants, the card and crown, the LOD0
    mesh (vertices, faces, UVs and slots, to the metadata's micrometre), and per slot its recipe and base map bytes."""
    digest = hashlib.sha256()

    def put(value):
        digest.update(json.dumps(value, sort_keys=True, separators=(",", ":")).encode("utf-8") + b"\n")

    put({"formula": FORMULA, "samples": SAMPLES, "transparent_bounces": TRANSPARENT_BOUNCES, "pad_px": PAD_PX,
         "keys": {k: record[k] for k in ("size_px", "views", "columns", "rows", "elevation_deg", "card", "crown")}})
    put([[round(c, 6) for c in v] for v in mesh.vertices])
    put([[indices, [[round(c, 6) for c in uv] for uv in uvs], slot] for indices, _, uvs, slot in mesh.faces])
    for slot in sorted(materials):
        m = materials[slot]
        put({"slot": slot, "visual": m["visual"], "recipe": m["recipe"]})
        for path in (m["texture"], m.get("mask")):
            digest.update((file_sha256(path) if path else "-").encode("ascii"))
    return digest.hexdigest()


def file_sha256(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def recorded(meta_path, atlas_path, inputs):
    """The impostor record the variant's current metadata holds, when it records these `inputs` and the atlas on disk
    still has its atlas_sha256; else None, and the atlas is rendered."""
    if not (os.path.isfile(meta_path) and os.path.isfile(atlas_path)):
        return None
    with open(meta_path, encoding="utf-8") as f:
        lods = json.load(f).get("lods", [])
    old = next((l["impostor"] for l in lods if isinstance(l, dict) and "impostor" in l), None)
    if not old or old.get("inputs_sha256") != inputs:
        return None
    return old if file_sha256(atlas_path) == old.get("atlas_sha256") else None


def layout(mesh, record, materials):
    """The VIEWS views of `mesh` laid side by side in the atlas plane, for one orthographic render: (vertices,
    faces, per-face UVs in tiles of each slot's base map, per-face slots, the atlas's side in metres). A vertex is
    (x right, y up, z towards the camera), metres, the atlas's lower-left corner at the origin."""
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
    faces, uvs, slots = [], [], []
    for k in range(VIEWS):
        for indices, _, face_uvs, slot in mesh.faces:
            tile = materials[slot]["tile_m"]
            faces.append([i + k * n for i in indices])
            uvs.append([(u / tile[0], v / tile[1]) for u, v in face_uvs])
            slots.append(slot)
    return vertices, faces, uvs, slots, COLUMNS * cell_w


def render(mesh, record, materials, path):
    """Renders the atlas to `path` in this Blender (module docstring), replacing the scene, and returns what two more
    renders of the same views measure (the module docstring): the crown's occlusion and its up share."""
    import bpy
    vertices, faces, uvs, slots, side = layout(mesh, record, materials)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    _settings(scene)
    me = bpy.data.meshes.new("impostor")
    me.from_pydata(vertices, [], faces)
    if me.validate(verbose=True):
        raise RuntimeError("impostor: Blender had to correct the laid-out mesh")
    order = sorted(set(slots))
    for slot in order:
        me.materials.append(_material(slot, materials[slot], "albedo"))
    layer = me.uv_layers.new(name="UVMap")
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
    _shoot(scene, path)
    size = png_header(path)
    if size != (SIZE_PX, SIZE_PX, 8, 6):
        raise RuntimeError(f"impostor: {path} came out {size}, expected {SIZE_PX}x{SIZE_PX} 8-bit RGBA")
    albedo = _pixels(path)
    occlusion, scratch = 1.0, path[:-len(".png")] + ".measure.png"
    try:
        if any(materials[slot]["mask"] for slot in order):
            for i, slot in enumerate(order):
                me.materials[i] = _material(slot, materials[slot], "occlusion")
            _shoot(scene, scratch)
            occlusion = _luminance(_pixels(scratch)) / _luminance(albedo)
        for i, slot in enumerate(order):
            me.materials[i] = _material(slot, materials[slot], "normal")
        _shoot(scene, scratch)
        up = up_share(albedo, _pixels(scratch), record)
    finally:
        if os.path.exists(scratch):
            os.remove(scratch)
    return occlusion, up


def card_normals(across, height, right, facing, crown, up=0.0):
    """The card's normal at card points `across` (metres along `right` from the axis) and `height` (metres up the
    card), numpy arrays, `right` and `facing` (the horizontal direction towards the viewer) per point: the crown's
    ellipsoid's on the facing side (a point beside its outline takes the outline's; one above or below the crown, a
    trunk's or stems', an upright cylinder's), leaning up by `up`, unit. The shader computes the same."""
    import numpy as np
    centre, (radius, half) = crown["centre"], crown["radii_m"]
    x = (across - (right[:, 0] * centre[0] + right[:, 2] * centre[2])) / radius
    y = (height - centre[1]) / half
    band = np.abs(y) < 1.0
    x, y = np.where(band, x, np.clip(x, -1.0, 1.0)), np.where(band, y, 0.0)
    out = np.maximum(np.sqrt(x * x + y * y), 1.0)
    x, y = x / out, y / out
    n = x[:, None] * right + y[:, None] * np.array([0.0, 1.0, 0.0]) + np.sqrt(np.maximum(1.0 - x * x - y * y, 0.0))[:, None] * facing
    n = n * math.sqrt(1.0 - up * up) + np.array([0.0, up, 0.0])
    return n / np.linalg.norm(n, axis=1, keepdims=True)


def up_share(albedo, normals, record):
    """How far the card's normal leans up: the share u in [0, MAX_UP] at which the card's mean sun response, its
    normal at each atlas pixel the shader's (card_normals), equals the mean response of the normals the views saw
    there (`normals`, the visible side's normal of the surface under the pixel, as the leaf shader lights a card),
    both weighted by the pixel's coverage times its albedo's luminance and summed over SUN_ELEVATIONS_DEG x VIEWS
    azimuths. Pixels are numpy rows (r, g, b, a) of the renders, bottom row first, as Blender stores an image."""
    import numpy as np
    weight = albedo[:, 3] * (_linear(albedo[:, :3]) @ LUMA)
    index = np.nonzero(weight > 0)[0]
    weight = weight[index]
    x_px, y_px = index % SIZE_PX, index // SIZE_PX
    k = (SIZE_PX - 1 - y_px) // CELL_PX[1] * COLUMNS + x_px // CELL_PX[0]
    axes = np.array([basis(j) for j in range(VIEWS)])  # (view, right / up / toward, xyz)
    right, up, toward = axes[k, 0], axes[k, 1], axes[k, 2]
    facing = toward * np.array([1.0, 0.0, 1.0])
    facing /= np.linalg.norm(facing, axis=1, keepdims=True)
    seen = _linear(normals[index, :3]) * 2.0 - 1.0  # the layout's axes are right, up, towards the camera
    seen = seen[:, :1] * right + seen[:, 1:2] * up + seen[:, 2:3] * toward
    card = record["card"]
    across = (x_px % CELL_PX[0] + 0.5) / CELL_PX[0] * card["width_m"] - card["width_m"] / 2
    height = card["bottom_m"] + (y_px % CELL_PX[1] + 0.5) / CELL_PX[1] * card["height_m"]
    suns = np.array([(math.sin(a) * math.cos(e), math.sin(e), math.cos(a) * math.cos(e))
                     for e in map(math.radians, SUN_ELEVATIONS_DEG)
                     for a in (2 * math.pi * j / VIEWS for j in range(VIEWS))])
    target = float(np.sum(weight[:, None] * np.maximum(seen @ suns.T, 0.0)))

    def response(u):
        lit = card_normals(across, height, right, facing, record["crown"], u) @ suns.T
        return float(np.sum(weight[:, None] * np.maximum(lit, 0.0)))

    lo, hi = 0.0, MAX_UP
    if response(lo) >= target:
        return lo
    if response(hi) <= target:
        return hi
    for _ in range(32):
        mid = (lo + hi) / 2
        lo, hi = (mid, hi) if response(mid) < target else (lo, mid)
    return (lo + hi) / 2


def _shoot(scene, path):
    import bpy
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    strip_png(path)


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


def _material(slot, m, mode):
    """Emission of the base map times the tint ("albedo"), times the mask's occlusion as well ("occlusion"), or of the
    visible side's normal mapped into 0-1 ("normal"); a clipped recipe is transparent where its alpha falls under the
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
    """(width, height, bit depth, colour type) from the PNG's IHDR; colour type 6 is RGBA."""
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
