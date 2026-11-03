"""The far layer (D-081; AST-10's far density layer): a ground patch's straws drawn into the base maps of its slab, so
they show beyond its micro mesh and at every LOD. The generator returns the record ("far_layer": its scatter, a
callable giving a Mesh of the kit in the straw palette's slot, the near and far slots, the patch's size and its
bake's radius and feather); this renders the scatter from straight above in headless Blender, orthographic over the
patch's square at the ground recipe's own base-map size, and lays it over that base map:

- the far map (`far_albedo`, the far slot's) wholly: a texel's straws over the ground by their coverage;
- the near map (`near_albedo`, the near slot's, LOD0's) faded by the bake's own density, 1 inside radius - feather,
  a smoothstep to 0 at the radius, 0 beyond: LOD0 shows its baked straws inside the bake and the painted ones beyond.

The render is flat, as the impostor's (lib.impostor): the palette's map times its tint, emitted, through the Standard
view transform over a transparent film, Cycles on the CPU with fixed samples and seed and no denoiser, so it repeats.
The blend is linear: a texel's colour is ground x (1 - a) + straw x a, the ground the recipe's base map and the
straw the render's colour over the ground slot's tint (the material multiplies the map by it), a the render's
coverage, both maps then encoded sRGB as the recipe's map is. Each map has its mask (`near_mask`, `far_mask`) laid
the same way over the ground recipe's packed mask, linear data as URP/Lit reads it: metallic R and B 0, occlusion G
the straw's 1 (the palette has no mask, so the baked straws take the ambient light unoccluded) and smoothness A the
palette recipe's constant, so a painted straw takes the ambient and the sky's reflection as a baked one does (under
overcast the ground mask's occlusion alone, 0.58 on average, left the far patch 14 % darker than LOD0 at 8 m,
measured). The maps are the generator's textures (lib.meta, roles "base_map" and "mask_map"), written beside the
metadata; the importer draws each slot with its recipe's material over its map and its mask. Nothing here is a mesh
of the asset: physics never reads the far layer."""
import os
import struct
import tempfile
import zlib

SAMPLES = 32  # Cycles samples per pixel: the coverage of a straw a fraction of a texel wide (lib.impostor's)
MATERIALS = os.path.join("unity", "Assets", "_Project", "Art", "Materials")
RECIPES = os.path.join("assets-src", "materials")
NEAR, FAR = "near_albedo", "far_albedo"
MASKS = {NEAR: "near_mask", FAR: "far_mask"}


def weight(r, radius, feather):
    """The bake's density `r` from the centre, as ground_patch's `kept` thins it: 1 inside radius - feather, a
    smoothstep down to 0 at the radius, 0 beyond (a hard rim at feather 0)."""
    if r > radius:
        return 0.0
    if r <= radius - feather:
        return 1.0
    t = (radius - r) / feather
    return t * t * (3 - 2 * t)


def recipe(root, visual):
    import json
    with open(os.path.join(root, RECIPES, visual + ".json"), encoding="utf-8") as f:
        return json.load(f)


def albedo(root, visual):
    """A derived albedo of the recipes the far layer reads (a ground recipe's base map, a palette's columns)."""
    from lib import impostor
    path = os.path.join(root, MATERIALS, f"{visual}_albedo.png")
    impostor._require_file(path, visual)
    return path


def render(root, record, visual_by_slot):
    """The two base maps of `record` (module docstring) as lib.meta textures with their PNG bytes, and a report
    line."""
    import bpy
    import numpy as np
    from lib import impostor
    near_slot, far_slot = record["near_slot"], record["far_slot"]
    ground = visual_by_slot[near_slot]
    if visual_by_slot[far_slot] != ground:
        raise ValueError(f"far layer: slot {far_slot} draws {visual_by_slot[far_slot]}, the near slot {ground}; the "
                         "far map lies over the near slot's recipe")
    ground_recipe = recipe(root, ground)
    if ground_recipe.get("kind") != "ground":
        raise ValueError(f"far layer: {ground} is not a ground recipe, whose base map covers the patch once")
    ground_tint = [impostor.gamma_to_linear(c) for c in ground_recipe["tint"][:3]]
    base_path = albedo(root, ground)
    mask_path = os.path.join(root, MATERIALS, f"{ground}_mask.png")
    impostor._require_file(mask_path, ground)
    mesh = record["scatter"]()
    slots = {slot for _, _, _, slot in mesh.faces}
    if len(slots) != 1:
        raise ValueError(f"far layer: the scatter draws in one palette slot, got {sorted(slots)}")
    straw_visual = visual_by_slot[slots.pop()]
    straw_recipe = recipe(root, straw_visual)
    if straw_recipe.get("kind") != "palette":
        raise ValueError(f"far layer: {straw_visual} is not a palette recipe")
    straw = {"texture": albedo(root, straw_visual), "cutoff": None, "alpha": 1.0,
             "tint": [impostor.gamma_to_linear(c) for c in straw_recipe["tint"][:3]]}
    straw_mask = (0.0, 1.0, 0.0, float(straw_recipe["smoothness"]))  # metallic, occlusion, -, smoothness

    bpy.ops.wm.read_factory_settings(use_empty=True)
    base = bpy.data.images.load(base_path)
    px = base.size[0]
    if base.size[1] != px:
        raise ValueError(f"far layer: {base_path} is {base.size[0]}x{base.size[1]}, not square")
    ground_px = np.empty(px * px * 4, dtype=np.float32)
    base.pixels.foreach_get(ground_px)
    bpy.data.images.remove(base)
    mask = bpy.data.images.load(mask_path)
    mask.colorspace_settings.name = 'Non-Color'
    if tuple(mask.size) != (px, px):
        raise ValueError(f"far layer: {mask_path} is {mask.size[0]}x{mask.size[1]}, not its base map's {px}x{px}")
    mask_px = np.empty(px * px * 4, dtype=np.float32)
    mask.pixels.foreach_get(mask_px)
    bpy.data.images.remove(mask)

    scene = bpy.context.scene
    impostor._settings(scene)
    scene.render.resolution_x = scene.render.resolution_y = px
    scene.cycles.samples = SAMPLES
    me = bpy.data.meshes.new("far layer")  # asset (x, y up, z) as Blender (x, z, y): seen from +y, right +x, up +z
    me.from_pydata([(x, z, y) for x, y, z in mesh.vertices], [], [f[0] for f in mesh.faces])
    me.materials.append(impostor._material("straw", straw, "albedo"))
    me.uv_layers.new(name=impostor.UV_MAP).data.foreach_set(
        "uv", [c for f in mesh.faces for uv in f[2] for c in uv])
    scene.collection.objects.link(bpy.data.objects.new("far layer", me))
    lo, hi = mesh.bounds()
    size = record["size_m"]
    camera = bpy.data.cameras.new("far layer")
    camera.type, camera.ortho_scale = 'ORTHO', size
    camera.clip_start, camera.clip_end = 0.01, hi[1] - lo[1] + 2.0
    camera_object = bpy.data.objects.new("far layer", camera)
    camera_object.location = (0.0, 0.0, hi[1] + 1.0)  # looking down -Z, up +Y: the image's rows run along +z
    scene.collection.objects.link(camera_object)
    scene.camera = camera_object
    folder = tempfile.mkdtemp(prefix="far_layer_")
    path = os.path.join(folder, "straws.png")
    try:
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        straws = impostor._pixels(path)  # bottom row first, encoded over 255, straight alpha
    finally:
        if os.path.exists(path):
            os.remove(path)
        os.rmdir(folder)

    rows = ground_px.reshape(px * px, 4).astype(np.float64)
    soil = _linear(rows[:, :3])
    colour = _linear(straws[:, :3]) / np.array(ground_tint)
    cover = straws[:, 3:4]
    centres = -size / 2 + (np.arange(px) + 0.5) * size / px
    x, z = np.meshgrid(centres, centres)  # row k is z = centres[k], bottom first as the pixels are
    w = np.vectorize(lambda v: weight(v, record["bake_radius_m"], record["bake_feather_m"]), otypes=[np.float64])(
        np.hypot(x, z).reshape(-1))[:, None]
    packed = mask_px.reshape(px * px, 4).astype(np.float64)
    textures, stats = [], {}
    for name, slot, a in ((NEAR, near_slot, cover * (1 - w)), (FAR, far_slot, cover)):
        laid = soil * (1 - a) + colour * a
        masked = packed * (1 - a) + np.array(straw_mask) * a
        for role, file, pixels in (("base_map", name, _encoded(laid)),
                                   ("mask_map", MASKS[name], np.clip(np.round(masked * 255.0), 0, 255).astype(np.uint8))):
            textures.append({"role": role, "slot": slot, "name": file, "uv_min": [-size / 2, -size / 2],
                             "uv_max": [size / 2, size / 2], "png": png(pixels.reshape(px, px, -1)[::-1])})
        stats[name] = " ".join(f"{c:.4f}" for c in laid.mean(axis=0)), masked[:, 1].mean()
    line = (f"far layer: {len(mesh.faces)} triangles rendered {px}x{px} over {size:g} m at {SAMPLES} samples, "
            f"straw coverage {cover.mean():.3f}; mean linear albedo ground {' '.join(f'{c:.4f}' for c in soil.mean(axis=0))}, "
            f"near {stats[NEAR][0]}, far {stats[FAR][0]}; mean occlusion ground {packed[:, 1].mean():.3f}, "
            f"near {stats[NEAR][1]:.3f}, far {stats[FAR][1]:.3f}")
    return textures, line


def _linear(encoded):
    import numpy as np
    return np.where(encoded <= 0.04045, encoded / 12.92, ((encoded + 0.055) / 1.055) ** 2.4)


def _encoded(linear):
    import numpy as np
    c = np.clip(linear, 0.0, 1.0)
    s = np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)
    return np.clip(np.round(s * 255.0), 0, 255).astype(np.uint8)


def png(pixels):
    """An 8-bit RGB or RGBA PNG of `pixels` (h x w x 3 or 4 bytes, top row first): IHDR, one IDAT of unfiltered rows
    at zlib level 9, IEND, so the same texels always make the same bytes."""
    h, w, channels = pixels.shape
    raw = b"".join(b"\x00" + pixels[y].tobytes() for y in range(h))

    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))

    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, {3: 2, 4: 6}[channels], 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))
