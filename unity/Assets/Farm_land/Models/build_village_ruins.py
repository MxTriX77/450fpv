# Builds ruined versions of the ALP country house for the Step 8 village. The pack's FBX files are only read.
#
#   Blender --background --factory-startup --python Assets/Farm_land/Models/build_village_ruins.py [-- preview_folder]
#
# Writes village_ruins.fbx next to this script, in metres, Y up, with these meshes:
#   Ruin_RoofCollapsed   the house with one corner fallen in, walls and roof together
#   Ruin_Walls           roofless, with broken walls about 1-3 m high and the stove chimney still standing
#   Ruin_Burnt           a burnt-out shell: higher broken walls on charred materials, chimney standing
#   Ruin_Stump           little more than the foundation, the stove and a corner of wall
#   Debris_RoofSheet_A/B pieces of the roof, for lying on the ground
#   Debris_WallPanel     a piece of fallen outer wall
#   Debris_Window        a loose window frame
#
# Every mesh in the house is cut into a fine grid, and faces above a broken height field are removed. Walls,
# windows and roof share one height field, so they break in the same places. Charred parts use material names
# ending in _Burnt, which Unity maps to darker copies of the house materials.
# With a preview folder, it also saves Workbench renders of each ruin there.

import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Vector, noise

OUTPUT_FOLDER = os.path.dirname(os.path.abspath(__file__))
OUTPUT_FBX = os.path.join(OUTPUT_FOLDER, "village_ruins.fbx")
PACK = os.path.normpath(os.path.join(OUTPUT_FOLDER, "../../ALP_Assets/country house01"))
SEED = 8120

# Grid cell size for the breaks. Wooden walls break along boards, so cells are narrow and short.
CELL_X = 0.45
CELL_Y = 0.45
CELL_Z = 0.3
HOUSE_TOP = 8.2

# Parts that never break, and parts that are always removed.
STOVE = "heating_stove"
REMOVED_PARTS = ("Windows01Glass",)
FOUNDATION_HEIGHT = 0.45

# Each ruin: walls stand at base height plus large-scale noise, minus collapses, with a jagged
# per-board edge. A collapse is (x, y, radius, height left at its centre). Heights are in metres.
RUINS = {
    "Ruin_RoofCollapsed": dict(base=20.0, variation=0.0, jag=0.5, burnt=False,
                               collapses=[(-5.0, 2.5, 5.5, 1.2), (6.5, -3.0, 2.5, 2.4)]),
    "Ruin_Walls": dict(base=2.3, variation=1.0, jag=0.6, burnt=False,
                       collapses=[(5.0, -2.5, 4.0, 0.6), (-6.5, 4.5, 3.0, 1.0)]),
    "Ruin_Burnt": dict(base=3.1, variation=0.8, jag=0.7, burnt=True,
                       collapses=[(-4.5, -3.0, 4.0, 0.8), (1.0, 5.0, 3.0, 1.6)]),
    "Ruin_Stump": dict(base=0.9, variation=0.7, jag=0.5, burnt=True,
                       collapses=[], corner=(-7.0, 4.6, 3.5, 2.8)),
}


def import_fbx(name):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(PACK, "Models", name))
    return [o for o in bpy.data.objects if o not in before]


def relink_textures():
    # The FBX points at the author's drive, so find each image by name in the pack's Textures folder.
    for image in bpy.data.images:
        base = os.path.splitext(os.path.basename(image.filepath.replace("\\", "/")))[0]
        for extension in (".png", ".tga", ".jpg"):
            candidate = os.path.join(PACK, "Textures", base + extension)
            if os.path.exists(candidate):
                image.filepath = candidate
                image.reload()
                break


def world_bmesh(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.transform(obj.matrix_world)
    return bm


def cut_grid(bm, low, high):
    """Splits every face along a grid of world-aligned planes."""
    def planes(axis, start, stop, step):
        normal = Vector((0, 0, 0))
        normal[axis] = 1.0
        value = math.floor(start / step) * step
        while value <= stop:
            geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
            co = Vector((0, 0, 0))
            co[axis] = value
            bmesh.ops.bisect_plane(bm, geom=geom, plane_co=co, plane_no=normal)
            value += step

    planes(0, low.x, high.x, CELL_X)
    planes(1, low.y, high.y, CELL_Y)
    planes(2, CELL_Z, min(high.z, HOUSE_TOP), CELL_Z)


def smoothstep(edge0, edge1, x):
    t = max(0.0, min(1.0, (x - edge0) / (edge1 - edge0)))
    return t * t * (3.0 - 2.0 * t)


def board_jitter(x, y, salt):
    # One random value per board column, so the broken edge steps like snapped planks.
    column = (round(x / CELL_X), round(y / CELL_Y), salt)
    return random.Random(hash(column) ^ SEED).random()


def wall_height(ruin, salt, x, y):
    height = ruin["base"] + ruin["variation"] * noise.noise(Vector((x * 0.22 + salt, y * 0.22, 0.5)))
    for cx, cy, radius, left in ruin["collapses"]:
        distance = math.hypot(x - cx, y - cy) / radius
        distance += 0.25 * noise.noise(Vector((x * 0.6, y * 0.6, salt + 3.0)))
        height = min(height, left + (height - left) * smoothstep(0.35, 1.0, distance))
    corner = ruin.get("corner")
    if corner:
        cx, cy, radius, top = corner
        reach = smoothstep(1.0, 0.3, math.hypot(x - cx, y - cy) / radius)
        height = max(height, height + (top - height) * reach)
    height -= ruin["jag"] * board_jitter(x, y, salt)
    return max(FOUNDATION_HEIGHT, height)


def break_part(bm, ruin, salt):
    removed = []
    for face in bm.faces:
        centre = face.calc_center_median()
        if centre.z > wall_height(ruin, salt, centre.x, centre.y):
            removed.append(face)
    bmesh.ops.delete(bm, geom=removed, context="FACES")
    # Drop tiny islands left floating by the cut, such as a single board end.
    remove_small_islands(bm, min_faces=3)


def remove_small_islands(bm, min_faces):
    seen = set()
    small = []
    for face in bm.faces:
        if face in seen:
            continue
        island = []
        stack = [face]
        seen.add(face)
        while stack:
            current = stack.pop()
            island.append(current)
            for edge in current.edges:
                for other in edge.link_faces:
                    if other not in seen:
                        seen.add(other)
                        stack.append(other)
        if len(island) < min_faces:
            small.extend(island)
    if small:
        bmesh.ops.delete(bm, geom=small, context="FACES")


def simplify(bm):
    # Merge the grid back into larger faces wherever nothing was removed, then triangulate here, since the
    # merged faces can be concave and Unity drops any it reads as self-intersecting.
    bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(0.5), use_dissolve_boundaries=False,
                             verts=bm.verts[:], edges=bm.edges[:], delimit={"NORMAL", "MATERIAL", "SEAM", "UV"})
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=0.0005)
    bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method="BEAUTY", ngon_method="EAR_CLIP")


def material_for(name, burnt):
    if not burnt:
        return bpy.data.materials[name]
    burnt_name = name + "_Burnt"
    material = bpy.data.materials.get(burnt_name)
    if material is None:
        material = bpy.data.materials[name].copy()
        material.name = burnt_name
    return material


def build_ruin(name, ruin, parts):
    mesh = bpy.data.meshes.new(name)
    combined = bmesh.new()
    materials = []
    for index, part in enumerate(parts):
        if part.name in REMOVED_PARTS:
            continue
        bm = world_bmesh(part)
        if part.name != STOVE:
            low = Vector([min(v.co[i] for v in bm.verts) for i in range(3)])
            high = Vector([max(v.co[i] for v in bm.verts) for i in range(3)])
            cut_grid(bm, low, high)
            break_part(bm, ruin, salt=index * 7.3)
            simplify(bm)
        if not bm.faces:
            bm.free()
            continue

        # Re-number this part's materials into the combined mesh.
        offset = {}
        for slot, source in enumerate(part.data.materials):
            material = material_for(source.name, ruin["burnt"])
            if material not in materials:
                materials.append(material)
            offset[slot] = materials.index(material)
        for face in bm.faces:
            face.material_index = offset.get(face.material_index, 0)

        temp = bpy.data.meshes.new("temp")
        bm.to_mesh(temp)
        bm.free()
        combined.from_mesh(temp)
        bpy.data.meshes.remove(temp)

    combined.to_mesh(mesh)
    combined.free()
    for material in materials:
        mesh.materials.append(material)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def cut_piece(name, part, low, high, burnt=False):
    """Copies the faces of a part whose centres fall inside a box, with the origin at their centre."""
    bm = world_bmesh(part)
    cut_grid(bm, low, high)
    outside = [f for f in bm.faces if not all(low[i] <= f.calc_center_median()[i] <= high[i] for i in range(3))]
    bmesh.ops.delete(bm, geom=outside, context="FACES")
    simplify(bm)
    centre = sum((v.co for v in bm.verts), Vector()) / len(bm.verts)
    bmesh.ops.translate(bm, verts=bm.verts[:], vec=-centre)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for source in part.data.materials:
        mesh.materials.append(material_for(source.name, burnt))
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def lay_flat(obj):
    """Rotates a piece so its largest face side lies on the ground, with its lowest point at zero."""
    mesh = obj.data
    normal = Vector()
    for polygon in mesh.polygons:
        normal += polygon.normal * polygon.area
    normal.normalize()
    rotation = normal.rotation_difference(Vector((0, 0, 1))).to_matrix().to_4x4()
    mesh.transform(rotation)
    lowest = min(v.co.z for v in mesh.vertices)
    for vertex in mesh.vertices:
        vertex.co.z -= lowest


def triangles(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def export_fbx(objects, path):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"MESH"},
        mesh_smooth_type="FACE",
        use_mesh_modifiers=False,
        add_leaf_bones=False,
        bake_anim=False,
        axis_forward="-Z",
        axis_up="Y",
        apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=True,
        path_mode="STRIP",
    )


def render_previews(objects, folder):
    os.makedirs(folder, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "TEXTURE"
    scene.render.resolution_x, scene.render.resolution_y = 800, 600
    camera = bpy.data.objects.new("PreviewCamera", bpy.data.cameras.new("PreviewCamera"))
    scene.collection.objects.link(camera)
    scene.camera = camera
    for obj in objects:
        for other in objects:
            other.hide_render = other is not obj
        target = Vector((0, -0.8, 1.5)) if obj.name.startswith("Ruin") else Vector((0, 0, 0.3))
        distance = 22.0 if obj.name.startswith("Ruin") else 5.0
        for view, (azimuth, elevation) in enumerate(((-60, 22), (120, 35))):
            a, e = math.radians(azimuth), math.radians(elevation)
            camera.location = target + distance * Vector((math.cos(e) * math.cos(a), math.cos(e) * math.sin(a), math.sin(e)))
            camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
            scene.render.filepath = os.path.join(folder, f"{obj.name}_{view}.png")
            bpy.ops.render.render(write_still=True)


bpy.ops.wm.read_factory_settings(use_empty=True)
house_parts = [o for o in import_fbx("House.fbx") if o.type == "MESH"]
house_parts.sort(key=lambda o: o.name)
relink_textures()
by_name = {o.name: o for o in house_parts}

results = [build_ruin(name, ruin, house_parts) for name, ruin in RUINS.items()]

roof = by_name["q9:Mesh1"]
walls = by_name["mainHouse01"]
debris = [
    cut_piece("Debris_RoofSheet_A", roof, Vector((-6.5, 1.6, 4.0)), Vector((-4.2, 4.6, 7.0))),
    cut_piece("Debris_RoofSheet_B", roof, Vector((4.4, -3.4, 4.0)), Vector((6.2, -1.6, 7.0))),
    cut_piece("Debris_WallPanel", walls, Vector((-7.2, -2.0, 0.5)), Vector((-6.6, 0.4, 2.4))),
]
for piece in debris:
    lay_flat(piece)

window = by_name["Window.001"]
window_copy = bpy.data.objects.new("Debris_Window", window.data.copy())
window_copy.data.transform(window.matrix_world)
bpy.context.scene.collection.objects.link(window_copy)
centre = sum((v.co for v in window_copy.data.vertices), Vector()) / len(window_copy.data.vertices)
for vertex in window_copy.data.vertices:
    vertex.co -= centre
lay_flat(window_copy)
debris.append(window_copy)

for obj in results + debris:
    print(f"{obj.name}: {triangles(obj)} triangles, materials {[m.name for m in obj.data.materials]}")

export_fbx(results + debris, OUTPUT_FBX)
print("Village ruins written to", OUTPUT_FBX)

if "--" in sys.argv and len(sys.argv) > sys.argv.index("--") + 1:
    for part in house_parts:
        part.hide_render = True
    render_previews(results + debris, sys.argv[sys.argv.index("--") + 1])
