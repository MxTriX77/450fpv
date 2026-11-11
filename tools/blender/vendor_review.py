"""The review sheet of a vendor plant (w4a-farmland-vegetation design 8; AST-12), rendered in the pinned Blender from
the local bundle tools/blender/vendor_plants.py writes (review-sheets/<ID>/_bundle/, git-ignored: the store's mesh
never leaves the machine).

    blender --background --factory-startup --python tools/blender/vendor_review.py -- <out folder> <bundle.json>...

Per variant and light (day, overcast) one PNG, <out folder>/<variant>_<light>.png, of rows of TILE px tiles, as Unity's review capture lays its
sheet out (ReviewCapture.cs: a 60 degree vertical field of view, the camera to the south-west, 20 degrees down for
the far shots, a 1.8 m scale bar in 0.1 m bands, a neutral 0.18 ground):
- row 1, as drawn: 1.5 m (pilot height, level), 10, 50 and 200 m;
- row 2, the turntable: four bearings round the plant, framed whole;
- row 3, in the day sheet only, the metadata over the mesh (the mesh pale): the capsules (wood brown, the sprays' branches orange) and the
  shell's pieces (green, half transparent) from two bearings; the wind volumes (blue frames) and the snag records
  (hooks red, edges yellow); the drawn samples (white points) the shell and the porosity were measured on.
EEVEE, the cards alpha-clipped at their material's cutoff and tinted by its base colour, as the friend's shader
draws them. Asset space is Unity's (lib.frame): a point (x, y, z) stands in Blender at (-x, -z, y)."""
import json
import math
import os
import sys

import bpy
import bmesh
from mathutils import Euler, Vector

TILE = 768
FOV_DEG = 60.0
AZIMUTH_DEG = 225.0
DOWN_DEG = 20.0
PILOT_M = 1.5
DISTANCES = (1.5, 10.0, 50.0, 200.0)
MARGIN_M = 0.3
GROUND = 0.18
BAR_M, BAND_M = 1.8, 0.1
LIGHTS = ("day", "overcast")
SAMPLES = 16  # EEVEE's samples per pixel (assumed: enough for alpha-clipped cards at 768 px)


def b(p):
    """A Unity point in Blender's frame."""
    return Vector((-p[0], -p[2], p[1]))


def reset(light="day"):
    """An empty scene in one of LIGHTS: day, a sun 40 degrees up under a blue sky; overcast, the sky alone, grey and
    brighter (assumed: the two profiles of Unity's review capture, approximated)."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.eevee.taa_render_samples = SAMPLES
    scene.render.resolution_x = scene.render.resolution_y = TILE
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("sky")
    world.use_nodes = True
    background = world.node_tree.nodes["Background"]
    background.inputs[0].default_value = (0.62, 0.7, 0.8, 1) if light == "day" else (0.75, 0.76, 0.78, 1)
    background.inputs[1].default_value = 0.9 if light == "day" else 1.6
    scene.world = world
    if light == "day":
        sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
        sun.data.energy = 3.5
        sun.rotation_euler = Euler((math.radians(50), 0, math.radians(30)))
        scene.collection.objects.link(sun)
    camera = bpy.data.objects.new("camera", bpy.data.cameras.new("camera"))
    camera.data.sensor_fit = "VERTICAL"
    camera.data.angle = math.radians(FOV_DEG)
    camera.data.clip_end = 2000
    scene.collection.objects.link(camera)
    scene.camera = camera
    return scene, camera


def material(name, color, alpha=1.0, image=None, cutoff=None, scale=(1, 1), offset=(0, 0)):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nodes, links = m.node_tree.nodes, m.node_tree.links
    bsdf = nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.9
    if image:
        tex = nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(image, check_existing=True)
        mapping = nodes.new("ShaderNodeMapping")
        coords = nodes.new("ShaderNodeTexCoord")
        mapping.inputs["Scale"].default_value = (scale[0], scale[1], 1)
        mapping.inputs["Location"].default_value = (offset[0], offset[1], 0)
        links.new(coords.outputs["UV"], mapping.inputs["Vector"])
        links.new(mapping.outputs["Vector"], tex.inputs["Vector"])
        tint = nodes.new("ShaderNodeMix")
        tint.data_type = "RGBA"
        tint.blend_type = "MULTIPLY"
        tint.inputs["Factor"].default_value = 1.0
        tint.inputs[7].default_value = (color[0], color[1], color[2], 1)
        links.new(tex.outputs["Color"], tint.inputs[6])
        links.new(tint.outputs[2], bsdf.inputs["Base Color"])
        if cutoff is not None:
            clip = nodes.new("ShaderNodeMath")
            clip.operation = "GREATER_THAN"
            clip.inputs[1].default_value = cutoff
            links.new(tex.outputs["Alpha"], clip.inputs[0])
            if alpha < 1:
                fade = nodes.new("ShaderNodeMath")
                fade.operation = "MULTIPLY"
                fade.inputs[1].default_value = alpha
                links.new(clip.outputs[0], fade.inputs[0])
                links.new(fade.outputs[0], bsdf.inputs["Alpha"])
            else:
                links.new(clip.outputs[0], bsdf.inputs["Alpha"])
        elif alpha < 1:
            bsdf.inputs["Alpha"].default_value = alpha
    else:
        bsdf.inputs["Base Color"].default_value = (color[0], color[1], color[2], 1)
        bsdf.inputs["Alpha"].default_value = alpha
    m.surface_render_method = "DITHERED"
    return m


def link(name, mesh_data, mat):
    obj = bpy.data.objects.new(name, mesh_data)
    obj.data.materials.append(mat)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def plant_mesh(bundle, pale):
    """The LOD0 mesh, a material per slot (its texture, cutoff and tint), or pale grey for the overlay rows."""
    points = [b(p) for p in bundle["points"]]
    mats = []
    for slot in bundle["slots"]:
        if pale:
            mats.append(material("pale_" + slot["name"], (0.85, 0.85, 0.85), 0.35 if slot["cutoff"] is not None
                                 else 0.45, slot["texture"], slot["cutoff"], slot["scale"], slot["offset"]))
        else:
            mats.append(material(slot["name"], [min(1.0, t) for t in slot["tint"]], 1.0, slot["texture"],
                                 slot["cutoff"], slot["scale"], slot["offset"]))
    data = bpy.data.meshes.new("plant")
    bm = bmesh.new()
    verts = [bm.verts.new(p) for p in points]
    uv = bm.loops.layers.uv.new("UVMap")
    for tri, corners in zip(bundle["tris"], bundle["uvs"]):
        a, bb, c, s = tri
        if len({a, bb, c}) < 3:
            continue
        try:
            face = bm.faces.new((verts[a], verts[c], verts[bb]))  # the mirror flips the winding
        except ValueError:
            continue
        face.material_index = s
        for loop, k in zip(face.loops, (0, 2, 1)):
            loop[uv].uv = corners[k]
    bm.to_mesh(data)
    bm.free()
    obj = bpy.data.objects.new("plant", data)
    for m in mats:
        obj.data.materials.append(m)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def ground():
    data = bpy.data.meshes.new("ground")
    bm = bmesh.new()
    half = 4000
    vs = [bm.verts.new((x, y, 0)) for x, y in ((-half, -half), (half, -half), (half, half), (-half, half))]
    bm.faces.new(vs)
    bm.to_mesh(data)
    bm.free()
    link("ground", data, material("ground", (GROUND, GROUND, GROUND)))


def bar(at):
    """The 1.8 m scale bar, black and white bands of 0.1 m, standing at a Unity point."""
    black, white = material("bar_black", (0.02, 0.02, 0.02)), material("bar_white", (0.95, 0.95, 0.95))
    for k in range(int(round(BAR_M / BAND_M))):
        data = bpy.data.meshes.new("band")
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        bm.to_mesh(data)
        bm.free()
        obj = link("band", data, black if k % 2 == 0 else white)
        obj.scale = (0.05, 0.05, BAND_M)
        obj.location = b((at[0], (k + 0.5) * BAND_M, at[2]))


def capsule_mesh(name, shapes, mat, segments=10):
    """Capsules and spheres of the metadata as one mesh."""
    data = bpy.data.meshes.new(name)
    bm = bmesh.new()
    for s in shapes:
        centre, r = Vector(b(s["center"])), s["radius"]
        if s["shape"] == "sphere":
            m = bmesh.ops.create_icosphere(bm, subdivisions=2, radius=r)
            bmesh.ops.translate(bm, verts=m["verts"], vec=centre)
            continue
        q = s["rotation"]  # Unity [x, y, z, w]; local +Y is the axis
        axis_u = _rotate(q, (0.0, 1.0, 0.0))
        axis = Vector(b(axis_u)) - Vector(b((0, 0, 0)))
        half = s["height"] / 2 - r
        a, c = centre - axis * half, centre + axis * half
        for end in (a, c):
            m = bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=6, radius=r)
            bmesh.ops.translate(bm, verts=m["verts"], vec=end)
        if half > 0:
            m = bmesh.ops.create_cone(bm, cap_ends=False, segments=segments, radius1=r, radius2=r, depth=2 * half)
            rot = Vector((0, 0, 1)).rotation_difference(axis).to_matrix().to_4x4()
            bmesh.ops.transform(bm, matrix=rot, verts=m["verts"])
            bmesh.ops.translate(bm, verts=m["verts"], vec=centre)
    bm.to_mesh(data)
    bm.free()
    return link(name, data, mat)


def _rotate(q, v):
    x, y, z, w = q
    vx, vy, vz = v
    tx, ty, tz = 2 * (y * vz - z * vy), 2 * (z * vx - x * vz), 2 * (x * vy - y * vx)
    return (vx + w * tx + (y * tz - z * ty), vy + w * ty + (z * tx - x * tz), vz + w * tz + (x * ty - y * tx))


def lines(name, records, mat, width):
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.bevel_depth = width
    for rec in records:
        spline = curve.splines.new("POLY")
        spline.points.add(len(rec) - 1)
        for point, p in zip(spline.points, rec):
            q = b(p)
            point.co = (q.x, q.y, q.z, 1)
    obj = bpy.data.objects.new(name, curve)
    obj.data.materials.append(mat)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def boxes(name, volumes, mat):
    records = []
    for v in volumes:
        c, s = v["center"], v["size"]
        lo = [c[k] - s[k] / 2 for k in range(3)]
        hi = [c[k] + s[k] / 2 for k in range(3)]
        corners = [(x, y, z) for x in (lo[0], hi[0]) for y in (lo[1], hi[1]) for z in (lo[2], hi[2])]
        for i in range(8):
            for j in range(i + 1, 8):
                if sum(corners[i][k] != corners[j][k] for k in range(3)) == 1:
                    records.append([corners[i], corners[j]])
    return lines(name, records, mat, 0.02)


def points(name, samples, mat, size=0.025):
    """The samples as small cubes, one mesh built at once (a bmesh cube per sample takes minutes for 40 000)."""
    h = size / 2
    corners = [(x, y, z) for x in (-h, h) for y in (-h, h) for z in (-h, h)]
    quads = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
    verts, faces = [], []
    for p in samples:
        q, base = b(p), len(verts)
        verts.extend((q.x + cx, q.y + cy, q.z + cz) for cx, cy, cz in corners)
        faces.extend(tuple(base + i for i in quad) for quad in quads)
    data = bpy.data.meshes.new(name)
    data.from_pydata(verts, [], faces)
    return link(name, data, mat)


def frame_whole(camera, lo, hi, azimuth, down):
    """The camera on a bearing round the plant, looking down `down` degrees at the middle of its box, as far as
    frames the box's bounding sphere."""
    centre = [(lo[k] + hi[k]) / 2 for k in range(3)]
    radius = math.dist(lo, hi) / 2
    d = radius / math.sin(math.radians(FOV_DEG) / 2) * 1.05
    aim(camera, centre, azimuth, down, d)


def aim(camera, target, azimuth, down, distance):
    """The camera `distance` from a Unity target on a bearing (degrees from north, clockwise), looking at it from
    `down` degrees above."""
    a, e = math.radians(azimuth), math.radians(down)
    eye = (target[0] + distance * math.cos(e) * math.sin(a), target[1] + distance * math.sin(e),
           target[2] + distance * math.cos(e) * math.cos(a))
    camera.location = b(eye)
    direction = b(target) - b(eye)
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    return eye


def distance_shot(camera, lo, hi, d):
    """Unity's review shots: 1.5 m at pilot height looking level, the others 20 degrees down at the box's middle;
    a camera inside the box plus the margin is pushed out along its ray."""
    centre = [(lo[k] + hi[k]) / 2 for k in range(3)]
    if d == DISTANCES[0]:
        target = (centre[0], PILOT_M, centre[2])
        down = 0.0
    else:
        target, down = centre, DOWN_DEG
    while True:
        eye = aim(camera, target, AZIMUTH_DEG, down, d)
        inside = all(lo[k] - MARGIN_M < eye[k] < hi[k] + MARGIN_M for k in (0, 2)) and eye[1] < hi[1] + MARGIN_M
        if not inside:
            return
        d += 0.25


def render(scene, path):
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def sheet(out, name, tiles):
    """The tiles (row-major, 4 per row) into <out>/<name>.png via Blender's image API."""
    import numpy as np
    rows = (len(tiles) + 3) // 4
    canvas = np.zeros((rows * TILE, 4 * TILE, 4), dtype=np.float32)
    canvas[..., 3] = 1
    for k, path in enumerate(tiles):
        img = bpy.data.images.load(path)
        px = np.array(img.pixels[:], dtype=np.float32).reshape(TILE, TILE, 4)
        r, c = k // 4, k % 4
        canvas[(rows - 1 - r) * TILE:(rows - r) * TILE, c * TILE:(c + 1) * TILE] = px
        bpy.data.images.remove(img)
    image = bpy.data.images.new(name, 4 * TILE, rows * TILE, alpha=True)
    image.pixels = canvas.ravel()
    image.filepath_raw = os.path.join(out, name + ".png")
    image.file_format = "PNG"
    image.save()
    return image.filepath_raw


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    out, bundles = argv[0], argv[1:]
    tmp = os.path.join(out, "_tiles")
    os.makedirs(tmp, exist_ok=True)
    for path in bundles:
        with open(path, encoding="utf-8") as f:
            bundle = json.load(f)
        variant, data = bundle["variant"], bundle["metadata"]
        lo, hi = data["bounds"]["min"], data["bounds"]["max"]
        bar_at = (hi[0] + 1.0, 0, (lo[2] + hi[2]) / 2)
        for light in LIGHTS:
            tiles = []

            def shoot():
                tiles.append(os.path.join(tmp, f"{variant}_{light}_{len(tiles)}.png"))
                render(scene, tiles[-1])

            # rows 1 and 2: as drawn
            scene, camera = reset(light)
            ground()
            plant_mesh(bundle, pale=False)
            bar(bar_at)
            for d in DISTANCES:
                distance_shot(camera, lo, hi, d)
                shoot()
            for k in range(4):
                frame_whole(camera, lo, hi, AZIMUTH_DEG + 90 * k, DOWN_DEG)
                shoot()
            if light == LIGHTS[0]:  # row 3: the metadata, once
                scene, camera = reset(light)
                ground()
                plant_mesh(bundle, pale=True)
                bar(bar_at)
                wood = [s for s in data["collision"] if s["material"] != "twig"]
                shell = [s for s in data["collision"] if s["material"] == "twig"]
                capsule_mesh("wood", wood, material("wood", (0.45, 0.2, 0.05)))
                shell_obj = capsule_mesh("shell", shell, material("shell", (0.1, 0.8, 0.2), 0.3))
                for k in (0, 1):
                    frame_whole(camera, lo, hi, AZIMUTH_DEG + 90 * k, 10.0)
                    shoot()
                shell_obj.hide_render = True
                boxes("wind", data["wind_volumes"], material("wind", (0.1, 0.3, 1.0)))
                lines("hooks", [r["points"] for r in data["snag"] if r["kind"] == "hook"],
                      material("hook", (1, 0.1, 0.1)), 0.012)
                lines("edges", [r["points"] for r in data["snag"] if r["kind"] == "edge"],
                      material("edge", (1, 0.9, 0.1)), 0.012)
                frame_whole(camera, lo, hi, AZIMUTH_DEG, 10.0)
                shoot()
                scene, camera = reset(light)
                ground()
                plant_mesh(bundle, pale=True)
                points("samples", bundle["samples"], material("samples", (1, 1, 1)))
                frame_whole(camera, lo, hi, AZIMUTH_DEG, 10.0)
                shoot()
            print(f"[review] {variant} {light}: {sheet(out, f'{variant}_{light}', tiles)}")


main()
