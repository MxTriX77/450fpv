"""The AST-12 review sheet of a vendor piece (w4b-farmland-ruins): its mesh as the friend's prefab places it, with its
pack textures (a burnt slot tinted as FarmLandBuilder's BurntTint), and its metadata over it. Rendered in Blender,
since the piece is never imported into our Unity project; composed in plain Python. Two steps, one command
(tools/vendor_asset.ps1 runs both):

    blender --background --factory-startup --python tools/blender/vendor_review.py -- <ID|variant ...|all>
    python tools/blender/vendor_review.py --compose <ID|variant ...|all>

Tiles, 1024 px, a 60 degree vertical field of view, on a neutral 0.18 ground with a 1.8 m scale bar (0.1 m bands)
beside the piece:
- distance: 1.5, 10, 50 and 200 m from the piece's bounds, from the south-west (azimuth 225 degrees); at the pilot's
  1.5 m height for 1.5 and 10 m, looking down 20 degrees for 50 and 200 m (as Fpv450.Review's capture);
- turntable: eight views round it, 20 degrees down, framing the bounds;
- overlay: the collision boxes coloured by physics material over the mesh (X-ray), the gaps cyan, the snag edges
  red, from the south-west and the north-east, and from above;
- gaps: for a piece with openings, each gap seen along its normal from 1.5 m and the four sides (and the top) from
  10 m, with the overlay (the spec: every opening's gap at 1.5 and 10 m).
Everything lands in review-sheets/<ID>/<variant>/ (git-ignored; the textures are the pack's) and the sheet in
review-sheets/<ID>/<variant>.png. Nothing is written into the repository's tracked files.
"""
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)

TILE = 1024
FOV_DEG = 60.0
DISTANCES_M = (1.5, 10.0, 50.0, 200.0)
PILOT_HEIGHT_M = 1.5
LOOK_DOWN_DEG = 20.0
AZIMUTH_DEG = 225.0  # from the piece towards the camera, measured from +Z (north) towards +X (east): south-west
BAR_HEIGHT_M, BAR_BAND_M, BAR_POST_M = 1.8, 0.1, 0.05
BURNT_TINT = (0.5, 0.46, 0.43)  # FarmLandBuilder.Village.cs, BurntTint
TEXTURES = {"OldHouseMapIndoorWall01": "OldHouseMapIndoorWall01.png", "OldHouseMapWood01": "OldHouseMapWood01.png",
            "OldHouseMapWood02": "OldHouseMapWood02.png", "WindowsMat01": "WindowMap01.png",
            "DoorsMap01": "DoorsMap01.png"}
PACK_TEXTURES = ("world_final", "Farming_land", "Assets", "ALP_Assets", "country house01", "Textures")
COLOURS = {"timber": (0.85, 0.45, 0.12), "masonry": (0.55, 0.65, 0.85), "asbestos_cement": (0.65, 0.3, 0.75),
           "steel": (0.3, 0.3, 0.3), "sheet_metal": (0.4, 0.4, 0.45)}
SHEETS = os.path.join(REPO_ROOT, "review-sheets")


def targets(names):
    from vendor_build import load, wanted
    assets = load(REPO_ROOT)
    return assets, wanted(assets, names)


def out_dir(asset_id, variant):
    return os.path.join(SHEETS, asset_id, variant)


# ------------------------------------------------------------------------------------------------- Blender side

def render(names):
    import bpy
    from lib import catalog, fbxread, frame
    from generators import vendor_ruin
    assets, pairs = targets(names)
    for asset_id, variant in pairs:
        entry = assets[asset_id]
        params = catalog.load_params(catalog.params_path(REPO_ROOT, entry, variant))
        meta_path = os.path.join(catalog.output_dir(REPO_ROOT, entry, asset_id), variant + ".assetmeta.json")
        with open(meta_path, encoding="utf-8") as f:
            meta = json.load(f)
        source = params["source"]
        mesh = vendor_ruin._read(source["model"], source["model_sha256"])[source["object"]]
        point, _ = vendor_ruin._placement(mesh["vertices"], params["prefab"])
        folder = out_dir(asset_id, variant)
        os.makedirs(folder, exist_ok=True)
        _scene_reset(bpy)
        piece = _piece(bpy, frame, variant, mesh, point)
        lo, hi = meta["bounds"]["min"], meta["bounds"]["max"]
        bar = _scale_bar(bpy, frame, (hi[0] + 0.6, 0.0, (lo[2] + hi[2]) / 2))
        ground = _ground(bpy)
        overlay = _overlay(bpy, frame, meta)
        tiles = []
        # distance tiles, textured
        _engine(bpy, textured=True)
        _show(overlay, False)
        centre = [(lo[k] + hi[k]) / 2 for k in range(3)]
        reach = math.hypot(hi[0] - lo[0], hi[2] - lo[2]) / 2
        for d in DISTANCES_M:
            az = math.radians(AZIMUTH_DEG)
            horizontal = reach + d
            height = PILOT_HEIGHT_M if d <= 10 else horizontal * math.tan(math.radians(LOOK_DOWN_DEG))
            eye = (centre[0] + horizontal * math.sin(az), height, centre[2] + horizontal * math.cos(az))
            look = (centre[0], min(centre[1], PILOT_HEIGHT_M) if d <= 10 else centre[1], centre[2])
            tiles.append(_shoot(bpy, frame, folder, f"distance_{d:g}m", eye, look, f"{d:g} m"))
        # turntable
        fit = _fit_distance(lo, hi)
        for k in range(8):
            az = math.radians(AZIMUTH_DEG + 45 * k)
            eye = (centre[0] + fit * math.cos(math.radians(LOOK_DOWN_DEG)) * math.sin(az),
                   centre[1] + fit * math.sin(math.radians(LOOK_DOWN_DEG)),
                   centre[2] + fit * math.cos(math.radians(LOOK_DOWN_DEG)) * math.cos(az))
            tiles.append(_shoot(bpy, frame, folder, f"turntable_{k}", eye, centre, f"turntable {45 * k} deg"))
        # overlay
        _engine(bpy, textured=False)
        _show(overlay, True)
        for name, az, down in (("overlay_sw", AZIMUTH_DEG, 30), ("overlay_ne", AZIMUTH_DEG + 180, 30),
                               ("overlay_top", AZIMUTH_DEG, 89)):
            a, e = math.radians(az), math.radians(down)
            eye = (centre[0] + fit * math.cos(e) * math.sin(a), centre[1] + fit * math.sin(e),
                   centre[2] + fit * math.cos(e) * math.cos(a))
            tiles.append(_shoot(bpy, frame, folder, name, eye, centre, name.replace("_", " ") + " (collision)"))
        # the gaps at 1.5 and 10 m
        if meta["gaps"]:
            for side, az in (("south", 180), ("west", 270), ("north", 0), ("east", 90)):
                a = math.radians(az)
                horizontal = reach + 10
                eye = (centre[0] + horizontal * math.sin(a), PILOT_HEIGHT_M + 1.0, centre[2] + horizontal * math.cos(a))
                tiles.append(_shoot(bpy, frame, folder, f"gaps_10m_{side}", eye, (centre[0], 1.6, centre[2]),
                                    f"gaps 10 m {side}"))
            eye = (centre[0], hi[1] + 10, centre[2] - 0.01)
            tiles.append(_shoot(bpy, frame, folder, "gaps_10m_top", eye, centre, "gaps 10 m above"))
            for g in meta["gaps"]:
                c = [sum(p[k] for p in g["polygon"]) / len(g["polygon"]) for k in range(3)]
                n = g["normal"]
                for sign, label in ((1, "a"), (-1, "b")):
                    eye = tuple(c[k] + sign * 1.5 * n[k] for k in range(3))
                    if eye[1] < 0.3 and sign < 0:
                        continue
                    tiles.append(_shoot(bpy, frame, folder, f"gap_{g['id']}_{label}", eye, c,
                                        f"{g['id']} 1.5 m ({'+' if sign > 0 else '-'}normal)"))
        with open(os.path.join(folder, "tiles.json"), "w", encoding="utf-8") as f:
            json.dump({"variant": variant, "tiles": tiles, "boxes": len(meta["collision"]),
                       "gaps": len(meta["gaps"])}, f, indent=1)
        print(f"{variant}: {len(tiles)} tiles in {os.path.relpath(folder, REPO_ROOT)}")


def _scene_reset(bpy):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.resolution_x = scene.render.resolution_y = TILE
    cam = bpy.data.objects.new("camera", bpy.data.cameras.new("camera"))
    cam.data.sensor_fit = "VERTICAL"
    cam.data.angle = math.radians(FOV_DEG)
    cam.data.clip_start, cam.data.clip_end = 0.05, 5000.0
    scene.collection.objects.link(cam)
    scene.camera = cam
    world = bpy.data.worlds.new("world")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.65, 0.8, 1.0)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.6
    scene.world = world
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sun.data.energy = 3.5
    sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(35))
    scene.collection.objects.link(sun)


def _engine(bpy, textured):
    scene = bpy.context.scene
    if textured:
        for name in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
            try:
                scene.render.engine = name
                break
            except TypeError:
                continue
        scene.view_settings.view_transform = "Standard"
    else:
        scene.render.engine = "BLENDER_WORKBENCH"
        shading = scene.display.shading
        shading.light = "STUDIO"
        shading.color_type = "MATERIAL"
        shading.show_xray = True
        shading.xray_alpha = 0.55
        shading.show_object_outline = True


def _material(bpy, name, colour=None, image=None, tint=(1.0, 1.0, 1.0)):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.85
    if image is not None:
        tex = m.node_tree.nodes.new("ShaderNodeTexImage")
        tex.image = image
        mix = m.node_tree.nodes.new("ShaderNodeMixRGB")
        mix.blend_type = "MULTIPLY"
        mix.inputs[0].default_value = 1.0
        mix.inputs[2].default_value = (*tint, 1.0)
        m.node_tree.links.new(tex.outputs["Color"], mix.inputs[1])
        m.node_tree.links.new(mix.outputs[0], bsdf.inputs["Base Color"])
        m.diffuse_color = (*[0.6 * t for t in tint], 1.0)
    else:
        bsdf.inputs["Base Color"].default_value = (*colour, 1.0)
        m.diffuse_color = (*colour, 1.0)
    return m


def _piece(bpy, frame, variant, mesh, point):
    folder = os.path.join(REPO_ROOT, "vendor", *PACK_TEXTURES)
    me = bpy.data.meshes.new(variant)
    verts = [frame.unity_to_blender(point(p)) for p in mesh["vertices"]]
    me.from_pydata(verts, [], [list(p) for p in mesh["polygons"]])
    slots = sorted(set(mesh["slots"]))
    for slot in slots:
        base = slot[:-len("_Burnt")] if slot.endswith("_Burnt") else slot
        image = bpy.data.images.load(os.path.join(folder, TEXTURES[base]), check_existing=True)
        me.materials.append(_material(bpy, slot, image=image,
                                      tint=BURNT_TINT if slot.endswith("_Burnt") else (1.0, 1.0, 1.0)))
    uv = me.uv_layers.new(name="uv")
    corner = 0
    for p, (poly, slot) in enumerate(zip(me.polygons, mesh["slots"])):
        poly.material_index = slots.index(slot)
        corners = mesh["uvs"][p] or [(0.0, 0.0)] * len(poly.vertices)
        for k in range(len(poly.vertices)):
            uv.data[poly.loop_start + k].uv = corners[k]
        corner += len(poly.vertices)
    ob = bpy.data.objects.new(variant, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def _ground(bpy):
    me = bpy.data.meshes.new("ground")
    s = 400.0
    me.from_pydata([(-s, -s, -0.002), (s, -s, -0.002), (s, s, -0.002), (-s, s, -0.002)], [], [(0, 1, 2, 3)])
    me.materials.append(_material(bpy, "ground", colour=(0.18, 0.18, 0.18)))
    ob = bpy.data.objects.new("ground", me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def _scale_bar(bpy, frame, foot):
    """A 1.8 m post of 0.1 m bands, black and white, standing on the ground at `foot` (asset space)."""
    dark, light = _material(bpy, "bar_dark", colour=(0.02, 0.02, 0.02)), _material(bpy, "bar_light", colour=(0.9, 0.9, 0.9))
    for k in range(int(round(BAR_HEIGHT_M / BAR_BAND_M))):
        _box(bpy, frame, f"bar_{k}", (foot[0], foot[1] + (k + 0.5) * BAR_BAND_M, foot[2]),
             (BAR_POST_M, BAR_BAND_M, BAR_POST_M), (0, 0, 0, 1), dark if k % 2 else light)


def _box(bpy, frame, name, center, size, rotation, material, collection=None):
    me = bpy.data.meshes.new(name)
    _box_into(me, frame, [(center, size, rotation)])
    me.materials.append(material)
    ob = bpy.data.objects.new(name, me)
    (collection or bpy.context.scene.collection).objects.link(ob)
    return ob


def _box_into(me, frame, boxes):
    verts, faces = [], []
    for center, size, q in boxes:
        base = len(verts)
        for sx in (-1, 1):
            for sy in (-1, 1):
                for sz in (-1, 1):
                    local = (sx * size[0] / 2, sy * size[1] / 2, sz * size[2] / 2)
                    p = _rotate(q, local)
                    verts.append(frame.unity_to_blender([center[k] + p[k] for k in range(3)]))
        faces += [(base + a, base + b, base + c, base + d) for a, b, c, d in
                  ((0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3))]
    me.from_pydata(verts, [], faces)


def _overlay(bpy, frame, meta):
    """The metadata as objects: one mesh of boxes per physics material, the gaps' polygons, the snag polylines."""
    col = bpy.data.collections.new("overlay")
    bpy.context.scene.collection.children.link(col)
    by_material = {}
    for c in meta["collision"]:
        by_material.setdefault(c["material"], []).append((c["center"], c["size"], c["rotation"]))
    for material, boxes in by_material.items():
        me = bpy.data.meshes.new("collision_" + material)
        _box_into(me, frame, boxes)
        me.materials.append(_material(bpy, "c_" + material, colour=COLOURS.get(material, (0.9, 0.9, 0.2))))
        ob = bpy.data.objects.new("collision_" + material, me)
        col.objects.link(ob)
    if meta["gaps"]:
        me = bpy.data.meshes.new("gaps")
        verts, faces = [], []
        for g in meta["gaps"]:
            base = len(verts)
            verts += [frame.unity_to_blender(p) for p in g["polygon"]]
            faces.append(tuple(range(base, base + len(g["polygon"]))))
        me.from_pydata(verts, [], faces)
        me.materials.append(_material(bpy, "gap", colour=(0.0, 0.95, 0.95)))
        col.objects.link(bpy.data.objects.new("gaps", me))
    if meta["snag"]:
        curve = bpy.data.curves.new("snag", "CURVE")
        curve.dimensions = "3D"
        curve.bevel_depth = 0.012
        for s in meta["snag"]:
            spline = curve.splines.new("POLY")
            spline.points.add(len(s["points"]) - 1)
            for k, p in enumerate(s["points"]):
                spline.points[k].co = (*frame.unity_to_blender(p), 1.0)
        curve.materials.append(_material(bpy, "snag", colour=(0.95, 0.05, 0.05)))
        col.objects.link(bpy.data.objects.new("snag", curve))
    return col


def _show(collection, visible):
    for ob in collection.objects:
        ob.hide_render = not visible


def _fit_distance(lo, hi):
    radius = 0.5 * math.sqrt(sum((hi[k] - lo[k]) ** 2 for k in range(3)))
    return max(radius / math.sin(math.radians(FOV_DEG / 2)) * 1.05, 2.0)


def _shoot(bpy, frame, folder, name, eye, look, label):
    from mathutils import Vector
    scene = bpy.context.scene
    cam = scene.camera
    cam.location = Vector(frame.unity_to_blender(eye))
    cam.rotation_euler = (Vector(frame.unity_to_blender(look)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    path = os.path.join(folder, name + ".png")
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return {"file": name + ".png", "label": label}


def _rotate(q, v):
    x, y, z, w = q
    t = (2 * (y * v[2] - z * v[1]), 2 * (z * v[0] - x * v[2]), 2 * (x * v[1] - y * v[0]))
    return (v[0] + w * t[0] + (y * t[2] - z * t[1]), v[1] + w * t[1] + (z * t[0] - x * t[2]),
            v[2] + w * t[2] + (x * t[1] - y * t[0]))


# --------------------------------------------------------------------------------------------- the compose side

def compose(names):
    from PIL import Image, ImageDraw
    _, pairs = targets(names)
    for asset_id, variant in pairs:
        folder = out_dir(asset_id, variant)
        with open(os.path.join(folder, "tiles.json"), encoding="utf-8") as f:
            index = json.load(f)
        main = [t for t in index["tiles"] if not t["file"].startswith("gap_")]
        gaps = [t for t in index["tiles"] if t["file"].startswith("gap_")]
        cell, cols = 512, 4
        rows = math.ceil(len(main) / cols) + math.ceil(len(gaps) / (cols * 2))
        sheet = Image.new("RGB", (cols * cell, 48 + rows * cell), (24, 24, 24))
        draw = ImageDraw.Draw(sheet)
        draw.text((12, 14), f"{asset_id} {variant}: {index['boxes']} collision boxes, {index['gaps']} gaps; colours: "
                            "timber orange, masonry blue, asbestos-cement purple, gaps cyan, snag red; bar 1.8 m",
                  fill=(240, 240, 240))
        y = 48
        for k, t in enumerate(main):
            x, yy = (k % cols) * cell, y + (k // cols) * cell
            sheet.paste(Image.open(os.path.join(folder, t["file"])).convert("RGB").resize((cell, cell)), (x, yy))
            draw.text((x + 8, yy + 8), t["label"], fill=(255, 255, 0))
        y += math.ceil(len(main) / cols) * cell
        small = cell // 2
        for k, t in enumerate(gaps):
            x, yy = (k % (cols * 2)) * small, y + (k // (cols * 2)) * small
            sheet.paste(Image.open(os.path.join(folder, t["file"])).convert("RGB").resize((small, small)), (x, yy))
            draw.text((x + 4, yy + 4), t["label"], fill=(255, 255, 0))
        path = os.path.join(SHEETS, asset_id, variant + ".png")
        sheet.save(path)
        print(f"{variant}: sheet {os.path.relpath(path, REPO_ROOT)} ({sheet.width}x{sheet.height})")


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    if argv and argv[0] == "--compose":
        compose(argv[1:])
    else:
        render(argv)
