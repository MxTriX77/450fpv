# Builds the reduced island tree from island_tree_01_4k.blend. The source file is never saved.
#
#   Blender --background Assets/Farm_land/Materials/island_tree_01_4k/island_tree_01_4k.blend \
#           --python Assets/Farm_land/Models/build_island_tree.py
#
# Writes these next to this script:
#   island_tree_reduced.fbx        meshes island_tree_LOD0, _LOD1 and _LOD2 (the impostor), and the same
#                                  three as island_tree_dry_LOD*, in metres, Y up
#   island_tree_leaves.png         3 x 3 atlas: each leaf cluster seen along its three local axes
#   island_tree_impostor.png       2 x 2 atlas: the reduced tree seen from four directions
#   island_tree_dry_leaves.png     the leaf atlas recoloured to dry leaves
#   island_tree_dry_impostor.png   the impostor atlas for the dry tree
#
# Decimating the leaves leaves holes, so each cluster becomes a few crossed cards showing a render of
# itself. Card normals point out of the crown rather than along the card, so the crown shades as a soft
# volume. Vertex colours hold occlusion and a small per-cluster tint, which Farm Land/Tree Leaves reads.

import math
import os
import random

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector, kdtree

OUTPUT_FOLDER = os.path.dirname(os.path.abspath(__file__))
SEED = 7411

# The source tree is 5 m tall. This gives about 12.5 m.
TREE_SCALE = 2.5

# The trunk stands on a soil plate. Faces below this source height are removed, and the rest of the plate
# sits below the origin so that it ends up under the terrain surface.
PLATE_CUT_HEIGHT = 0.02
GROUND_HEIGHT = 0.04

BARK_TRIS = (8000, 1500)
TWIG_RATIO = (0.35, 0.08)

# Cards show the same leaves from each side, so they read thinner than the source clusters. Enlarging them
# fills the crown back out at no triangle cost.
LOD0_LEAF_ENLARGE = 1.25
LOD1_LEAF_SHARE = 0.5
LOD1_LEAF_ENLARGE = 1.7

LEAF_ATLAS_SIZE = 2048
LEAF_ATLAS_COLUMNS = 3
IMPOSTOR_ATLAS_SIZE = 2048
IMPOSTOR_ATLAS_COLUMNS = 2
IMPOSTOR_ANGLES = (0.0, 45.0, 90.0, 135.0)
CARD_MARGIN = 1.04

# The radius used to find which way a cluster faces out of its part of the crown, in source metres.
CROWN_LOBE_RADIUS = 0.8
OCCLUSION_DARKEST = 0.6
CROWN_NORMAL_UP = 0.5
TINT_VARIATION = 0.14

# Multiplies the leaf atlas, whose source comes out a dull olive. The impostor is baked from the atlas, so it
# follows. Keep this muted: the map is dry and neglected, and greener values quickly read as lime.
LEAF_ATLAS_TINT = (1.05, 1.14, 0.92)

# The dry tree keeps this share of its clusters, so bare branches show, and they vary more in colour. Its atlas
# maps the source leaves onto a ramp from brown to straw, using how green each pixel was. Saturated versions of
# these read as autumn colours rather than a tree dying of drought.
DRY_LEAF_SHARE = 0.6
DRY_TINT_VARIATION = 0.3
DRY_LEAF_BROWN = np.array((0.36, 0.28, 0.19), dtype=np.float32)
DRY_LEAF_STRAW = np.array((0.60, 0.52, 0.36), dtype=np.float32)

# Material slots, which become submeshes in Unity. The prefab builder matches them by these names.
BARK, TWIGS, LEAVES = 0, 1, 2
MATERIAL_NAMES = ("IslandTree_Bark", "IslandTree_Twigs", "IslandTree_Leaves")
SOURCE_IMAGES = {
    BARK: "island_tree_01_diff.png",
    TWIGS: "island_tree_01_branches_diff.png",
    LEAVES: "island_tree_01_leaves_diff.png",
}
LEAF_ALPHA_IMAGE = "island_tree_01_leaves_alpha.png"

# Corner positions along a card's two axes, and which way the camera looks to see each card straight on.
# Each row is (card U axis, card V axis, camera rotation in degrees).
LEAF_VIEWS = (
    (Vector((1, 0, 0)), Vector((0, 1, 0)), (0, 0, 0)),
    (Vector((1, 0, 0)), Vector((0, 0, 1)), (90, 0, 0)),
    (Vector((0, 1, 0)), Vector((0, 0, 1)), (90, 0, 90)),
)
CARD_CORNERS = ((-1, -1), (1, -1), (1, 1), (-1, 1))


def collect_instances():
    """Returns (source object name, matrix in tree space) for every leaf cluster and branch piece."""
    tree = bpy.data.objects["island_tree_01_geometry_nodes"]
    group = tree.modifiers[0].node_group
    output = next(n for n in group.nodes if n.bl_idname == "NodeGroupOutput")
    join = next(n for n in group.nodes if n.bl_idname == "GeometryNodeJoinGeometry")

    # The graph ends in Realize Instances, which would hide the transforms.
    group.links.new(join.outputs[0], output.inputs[0])
    bpy.context.view_layer.update()

    to_tree = tree.matrix_world.inverted()
    leaves, twigs = [], []
    for instance in bpy.context.evaluated_depsgraph_get().object_instances:
        if not instance.is_instance:
            continue
        name = instance.object.name
        matrix = to_tree @ instance.matrix_world
        (leaves if "_leaves_" in name else twigs).append((name, matrix.copy()))

    # Depsgraph order isn't guaranteed, and the random choices below must not depend on it.
    leaves.sort(key=lambda item: tuple(item[1].translation))
    twigs.sort(key=lambda item: tuple(item[1].translation))
    return leaves, twigs


def local_bounds(mesh):
    points = [v.co for v in mesh.vertices]
    low = Vector(min(p[i] for p in points) for i in range(3))
    high = Vector(max(p[i] for p in points) for i in range(3))
    return low, high


def decimated_copy(mesh, ratio):
    """Returns a collapse-decimated copy of a mesh, which keeps UV seams in place."""
    obj = bpy.data.objects.new("decimate", mesh)
    bpy.context.scene.collection.objects.link(obj)
    modifier = obj.modifiers.new("decimate", "DECIMATE")
    modifier.ratio = ratio
    bpy.context.view_layer.update()
    evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    result = bpy.data.meshes.new_from_object(evaluated)
    bpy.data.objects.remove(obj)
    return result


def bark_mesh(target_tris):
    source = bpy.data.objects["island_tree_01_geometry_nodes"].data.copy()
    bm = bmesh.new()
    bm.from_mesh(source)
    plate = [f for f in bm.faces if all(v.co.z < PLATE_CUT_HEIGHT for v in f.verts)]
    bmesh.ops.delete(bm, geom=plate, context="FACES")
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    count = len(bm.faces)
    bm.to_mesh(source)
    bm.free()
    return decimated_copy(source, min(1.0, target_tris / count))


def build_wood(lod, twig_instances):
    """Joins the trunk and branch pieces, with their shaded normals stored in a corner attribute."""
    bark = bark_mesh(BARK_TRIS[lod])
    reduced = {}

    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    bm.from_mesh(bark)
    for face in bm.faces:
        face.material_index = BARK

    for name, matrix in twig_instances:
        # The source LOD1 pieces are already clean, so they reduce further than LOD0 would.
        source_name = name.replace("_LOD0", "_LOD1")
        if source_name not in reduced:
            reduced[source_name] = decimated_copy(bpy.data.objects[source_name].data.copy(), TWIG_RATIO[lod])
        before = len(bm.faces)
        bm.from_mesh(reduced[source_name])
        bm.faces.ensure_lookup_table()
        added = bm.faces[before:]
        for face in added:
            face.material_index = TWIGS
        verts = list({v for f in added for v in f.verts})
        bmesh.ops.transform(bm, matrix=matrix, verts=verts)

    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new("wood")
    bm.to_mesh(mesh)
    bm.free()

    mesh.shade_smooth()
    normals = [0.0] * (len(mesh.loops) * 3)
    mesh.corner_normals.foreach_get("vector", normals)
    mesh.attributes.new("shaded_normal", "FLOAT_VECTOR", "CORNER").data.foreach_set("vector", normals)
    white = [1.0] * (len(mesh.loops) * 4)
    mesh.attributes.new("Col", "FLOAT_COLOR", "CORNER").data.foreach_set("color", white)
    return mesh


def crown_shading(leaf_instances):
    """Returns (crown centre, per-cluster outward direction, per-cluster occlusion from 0 to 1)."""
    positions = [matrix.translation.copy() for _, matrix in leaf_instances]
    centre = sum(positions, Vector()) / len(positions)

    tree = kdtree.KDTree(len(positions))
    for i, position in enumerate(positions):
        tree.insert(position, i)
    tree.balance()

    outward, occlusion = [], []
    for position in positions:
        nearby = tree.find_range(position, CROWN_LOBE_RADIUS)
        offset = position - sum((p for p, _, _ in nearby), Vector()) / len(nearby)
        exposure = min(1.0, offset.length / (CROWN_LOBE_RADIUS * 0.4))
        lobe = offset.normalized() if offset.length > 1e-4 else Vector((0, 0, 1))
        outward.append((position - centre).normalized() * 0.6 + lobe + Vector((0, 0, CROWN_NORMAL_UP)))

        # Clusters inside the crown, and those low in it, see less sky.
        height = (position.z - centre.z) * 0.2 + 0.85
        occlusion.append(OCCLUSION_DARKEST + (1 - OCCLUSION_DARKEST) * exposure * max(0.0, min(1.0, height)))
    return centre, outward, occlusion


def cluster_card(bm, uv, color, normal_layer, name, matrix, view_index, enlarge, tint, facing):
    """Adds one card for a cluster, showing that cluster's atlas cell for this view."""
    low, high = local_bounds(bpy.data.objects[name].data)
    centre = (low + high) * 0.5
    axis_u, axis_v, _ = LEAF_VIEWS[view_index]
    size = max((high - low).dot(axis_u), (high - low).dot(axis_v)) * CARD_MARGIN * enlarge

    cluster_index = LEAF_NAMES.index(name)
    cell = 1.0 / LEAF_ATLAS_COLUMNS
    inset = 1.0 / LEAF_ATLAS_SIZE
    cell_x, cell_y = view_index * cell, cluster_index * cell

    verts = []
    for sign_u, sign_v in CARD_CORNERS:
        local = centre + axis_u * (sign_u * size * 0.5) + axis_v * (sign_v * size * 0.5)
        verts.append(bm.verts.new(matrix @ local))
    face = bm.faces.new(verts)
    face.material_index = LEAVES
    face.smooth = True

    for loop, (sign_u, sign_v) in zip(face.loops, CARD_CORNERS):
        loop[uv].uv = (
            cell_x + inset + (cell - 2 * inset) * (sign_u * 0.5 + 0.5),
            cell_y + inset + (cell - 2 * inset) * (sign_v * 0.5 + 0.5),
        )
        loop[color] = tint
        corner = matrix.to_3x3() @ (axis_u * sign_u + axis_v * sign_v)
        loop[normal_layer] = (facing + corner.normalized() * 0.25).normalized()


def build_leaf_cards(lod, leaf_instances, facing, occlusion, kept, variation):
    rng = random.Random(SEED + lod)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    color = bm.loops.layers.float_color.new("Col")
    normal_layer = bm.loops.layers.float_vector.new("shaded_normal")

    for i, (name, matrix) in enumerate(leaf_instances):
        share = rng.random()
        value = 1 + (rng.random() - 0.5) * variation * 2
        warm = rng.random() * variation
        shade = occlusion[i] * value
        tint = (shade * (1 + warm * 0.5), shade, shade * (1 - warm), 1.0)

        # Drawn after the random values, so that kept clusters look the same with or without this.
        if i not in kept:
            continue

        if lod == 0:
            views, enlarge = (0, 1, 2), LOD0_LEAF_ENLARGE
        else:
            if share > LOD1_LEAF_SHARE:
                continue
            views, enlarge = (0, rng.choice((1, 2))), LOD1_LEAF_ENLARGE

        for view in views:
            cluster_card(bm, uv, color, normal_layer, name, matrix, view, enlarge, tint, facing[i])

    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new("cards")
    bm.to_mesh(mesh)
    bm.free()
    return mesh


def finish_lod(name, parts):
    """Joins mesh parts, applies the final scale and origin, and stores the shaded normals."""
    bm = bmesh.new()
    for part in parts:
        bm.from_mesh(part)
    root = Matrix.Scale(TREE_SCALE, 4) @ Matrix.Translation((0, 0, -GROUND_HEIGHT))
    bmesh.ops.transform(bm, matrix=root, verts=bm.verts[:])

    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    return finish_mesh(name, mesh)


def finish_mesh(name, mesh):
    for material_name in MATERIAL_NAMES:
        mesh.materials.append(bpy.data.materials.get(material_name) or bpy.data.materials.new(material_name))
    mesh.shade_smooth()

    normals = [0.0] * (len(mesh.loops) * 3)
    mesh.attributes["shaded_normal"].data.foreach_get("vector", normals)
    triples = [Vector(normals[i:i + 3]).normalized() for i in range(0, len(normals), 3)]
    mesh.normals_split_custom_set(triples)
    mesh.attributes.remove(mesh.attributes["shaded_normal"])
    mesh.color_attributes.active_color = mesh.color_attributes["Col"]

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def bake_scene(size):
    scene = bpy.data.scenes.new("bake")
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 32
    scene.cycles.use_denoising = False
    # A view through the crown crosses many cards, and running out of bounces renders black.
    scene.cycles.max_bounces = 128
    scene.cycles.transparent_max_bounces = 128
    scene.render.film_transparent = True
    scene.render.resolution_x = scene.render.resolution_y = size
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"

    camera = bpy.data.objects.new("bake_camera", bpy.data.cameras.new("bake_camera"))
    camera.data.type = "ORTHO"
    camera.data.clip_end = 100.0
    scene.collection.objects.link(camera)
    scene.camera = camera
    return scene, camera


def render_cell(scene, path):
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True, scene=scene.name)
    image = bpy.data.images.load(path)
    image.colorspace_settings.name = "Non-Color"
    size = image.size[0]
    pixels = np.empty(size * size * 4, dtype=np.float32)
    image.pixels.foreach_get(pixels)
    bpy.data.images.remove(image)
    os.remove(path)
    return pixels.reshape(size, size, 4)


def save_atlas(atlas, path):
    size = atlas.shape[0]
    image = bpy.data.images.new("atlas", size, size, alpha=True)
    image.colorspace_settings.name = "Non-Color"
    image.alpha_mode = "STRAIGHT"
    image.pixels.foreach_set(atlas.reshape(-1))
    image.filepath_raw = path
    image.file_format = "PNG"
    image.save()
    bpy.data.images.remove(image)


def baked_material(name, image, alpha_image=None, use_alpha_channel=False, vertex_colour=False, occlusion_distance=0.0):
    """Unlit material for baking, so the result holds colour only and Unity adds the light."""
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes, links = material.node_tree.nodes, material.node_tree.links
    nodes.clear()

    output = nodes.new("ShaderNodeOutputMaterial")
    emission = nodes.new("ShaderNodeEmission")
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = image
    colour = texture.outputs["Color"]

    if vertex_colour:
        attribute = nodes.new("ShaderNodeVertexColor")
        attribute.layer_name = "Col"
        multiply = nodes.new("ShaderNodeVectorMath")
        multiply.operation = "MULTIPLY"
        links.new(colour, multiply.inputs[0])
        links.new(attribute.outputs["Color"], multiply.inputs[1])
        colour = multiply.outputs["Vector"]

    if occlusion_distance > 0:
        occlusion = nodes.new("ShaderNodeAmbientOcclusion")
        occlusion.inputs["Distance"].default_value = occlusion_distance
        links.new(colour, occlusion.inputs["Color"])
        colour = occlusion.outputs["Color"]

    links.new(colour, emission.inputs["Color"])
    surface = emission.outputs[0]

    if alpha_image is not None or use_alpha_channel:
        mix = nodes.new("ShaderNodeMixShader")
        transparent = nodes.new("ShaderNodeBsdfTransparent")
        if alpha_image is not None:
            alpha_texture = nodes.new("ShaderNodeTexImage")
            alpha_texture.image = alpha_image
            alpha_texture.image.colorspace_settings.name = "Non-Color"
            links.new(alpha_texture.outputs["Color"], mix.inputs[0])
        else:
            links.new(texture.outputs["Alpha"], mix.inputs[0])
        links.new(transparent.outputs[0], mix.inputs[1])
        links.new(surface, mix.inputs[2])
        surface = mix.outputs[0]

    links.new(surface, output.inputs["Surface"])
    return material


def bake_leaf_atlas(path, dry_path):
    images = bpy.data.images
    leaf_material = baked_material(
        "bake_leaves", images[SOURCE_IMAGES[LEAVES]], alpha_image=images[LEAF_ALPHA_IMAGE], occlusion_distance=0.04)
    twig_material = baked_material("bake_twigs", images[SOURCE_IMAGES[TWIGS]], occlusion_distance=0.04)

    cell = LEAF_ATLAS_SIZE // LEAF_ATLAS_COLUMNS
    scene, camera = bake_scene(cell)
    atlas = np.zeros((LEAF_ATLAS_SIZE, LEAF_ATLAS_SIZE, 4), dtype=np.float32)

    for row, name in enumerate(LEAF_NAMES):
        source = bpy.data.objects[name]
        mesh = source.data.copy()
        mesh.materials[0] = leaf_material
        mesh.materials[1] = twig_material
        obj = bpy.data.objects.new("bake_cluster", mesh)
        scene.collection.objects.link(obj)

        low, high = local_bounds(mesh)
        centre = (low + high) * 0.5
        for column, (axis_u, axis_v, rotation) in enumerate(LEAF_VIEWS):
            size = max((high - low).dot(axis_u), (high - low).dot(axis_v)) * CARD_MARGIN
            view = axis_u.cross(axis_v)
            camera.location = centre + view * 5.0
            camera.rotation_euler = [math.radians(a) for a in rotation]
            camera.data.ortho_scale = size
            pixels = render_cell(scene, os.path.join(OUTPUT_FOLDER, "_bake_cell.png"))
            atlas[row * cell:(row + 1) * cell, column * cell:(column + 1) * cell] = pixels

        scene.collection.objects.unlink(obj)
        bpy.data.objects.remove(obj)

    save_atlas(dry_leaf_colours(atlas), dry_path)
    atlas[..., :3] = np.minimum(atlas[..., :3] * np.array(LEAF_ATLAS_TINT, dtype=np.float32), 1.0)
    save_atlas(atlas, path)
    bpy.data.scenes.remove(scene)


def dry_leaf_colours(atlas):
    """Returns a copy with leaves moved onto the brown to straw ramp, keeping their light and dark detail."""
    result = atlas.copy()
    red, green = atlas[..., 0], atlas[..., 1]
    brightness = atlas[..., :3].mean(axis=2)
    solid = atlas[..., 3] > 0.5
    detail = brightness / max(float(brightness[solid].mean()), 1e-3)
    share = np.clip((green - red) * 6.0 + 0.5, 0.0, 1.0)[..., np.newaxis]
    colour = DRY_LEAF_BROWN * (1 - share) + DRY_LEAF_STRAW * share
    result[..., :3] = np.minimum(colour * detail[..., np.newaxis], 1.0)
    return result


def bake_impostor_atlas(lod0, path, leaf_atlas_path):
    images = bpy.data.images
    atlas_image = images.load(leaf_atlas_path)
    atlas_image.alpha_mode = "STRAIGHT"
    bake_materials = (
        baked_material("bake_bark", images[SOURCE_IMAGES[BARK]], vertex_colour=True),
        baked_material("bake_impostor_twigs", images[SOURCE_IMAGES[TWIGS]], vertex_colour=True),
        baked_material("bake_impostor_leaves", atlas_image, use_alpha_channel=True, vertex_colour=True),
    )

    mesh = lod0.data.copy()
    for i, material in enumerate(bake_materials):
        mesh.materials[i] = material
    obj = bpy.data.objects.new("bake_tree", mesh)

    low, high = local_bounds(mesh)
    centre_xy = Vector(((low.x + high.x) * 0.5, (low.y + high.y) * 0.5, 0))
    radius = max(math.hypot(v.co.x - centre_xy.x, v.co.y - centre_xy.y) for v in mesh.vertices)
    size = max(radius * 2, high.z) * CARD_MARGIN

    cell = IMPOSTOR_ATLAS_SIZE // IMPOSTOR_ATLAS_COLUMNS
    scene, camera = bake_scene(cell)
    scene.collection.objects.link(obj)
    atlas = np.zeros((IMPOSTOR_ATLAS_SIZE, IMPOSTOR_ATLAS_SIZE, 4), dtype=np.float32)

    for i, angle in enumerate(IMPOSTOR_ANGLES):
        theta = math.radians(angle)
        look = Vector((-math.sin(theta), math.cos(theta), 0))
        camera.location = centre_xy + Vector((0, 0, size * 0.5)) - look * 100.0
        camera.rotation_euler = (math.radians(90), 0, theta)
        camera.data.ortho_scale = size
        camera.data.clip_end = 300.0
        column, row = i % IMPOSTOR_ATLAS_COLUMNS, i // IMPOSTOR_ATLAS_COLUMNS
        pixels = render_cell(scene, os.path.join(OUTPUT_FOLDER, "_bake_cell.png"))
        atlas[row * cell:(row + 1) * cell, column * cell:(column + 1) * cell] = pixels

    save_atlas(atlas, path)
    scene.collection.objects.unlink(obj)
    bpy.data.objects.remove(obj)
    bpy.data.scenes.remove(scene)
    return centre_xy, size


def build_impostor(name, centre_xy, size):
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    color = bm.loops.layers.float_color.new("Col")
    normal_layer = bm.loops.layers.float_vector.new("shaded_normal")
    cell = 1.0 / IMPOSTOR_ATLAS_COLUMNS

    for i, angle in enumerate(IMPOSTOR_ANGLES):
        theta = math.radians(angle)
        axis_u = Vector((math.cos(theta), math.sin(theta), 0))
        cell_x, cell_y = (i % IMPOSTOR_ATLAS_COLUMNS) * cell, (i // IMPOSTOR_ATLAS_COLUMNS) * cell

        verts = []
        for sign_u, sign_v in CARD_CORNERS:
            verts.append(bm.verts.new(centre_xy + axis_u * (sign_u * size * 0.5) + Vector((0, 0, size * (sign_v * 0.5 + 0.5)))))
        face = bm.faces.new(verts)
        face.material_index = LEAVES
        face.smooth = True

        for loop, (sign_u, sign_v) in zip(face.loops, CARD_CORNERS):
            loop[uv].uv = (cell_x + cell * (sign_u * 0.5 + 0.5), cell_y + cell * (sign_v * 0.5 + 0.5))
            loop[color] = (1.0, 1.0, 1.0, 1.0)
            # Soft outward normals that stay upward lit, since a card has no real volume.
            loop[normal_layer] = (axis_u * (sign_u * 0.5) + Vector((0, 0, 0.8 + sign_v * 0.2))).normalized()

    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    return finish_mesh(name, mesh)


def export_fbx(objects, path):
    for obj in bpy.context.scene.objects:
        obj.select_set(obj in objects)
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"MESH"},
        mesh_smooth_type="OFF",
        use_mesh_modifiers=False,
        add_leaf_bones=False,
        bake_anim=False,
        axis_forward="-Z",
        axis_up="Y",
        apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=True,
        colors_type="LINEAR",
        path_mode="STRIP",
    )


def build_variant(prefix, leaf_atlas_path, impostor_atlas_path, kept, variation):
    """Builds the three LOD objects of one tree, and bakes its impostor atlas from the finished LOD0."""
    lods = []
    for lod in (0, 1):
        wood = build_wood(lod, twig_instances)
        cards = build_leaf_cards(lod, leaf_instances, facing, occlusion, kept, variation)
        lods.append(finish_lod(f"{prefix}_LOD{lod}", [wood, cards]))
        triangles = sum(len(p.vertices) - 2 for p in lods[-1].data.polygons)
        print(f"{prefix}_LOD{lod}: {triangles} triangles")

    centre_xy, card_size = bake_impostor_atlas(lods[0], impostor_atlas_path, leaf_atlas_path)
    lods.append(build_impostor(f"{prefix}_LOD2", centre_xy, card_size))
    return lods


def output_path(name):
    return os.path.join(OUTPUT_FOLDER, name)


leaf_instances, twig_instances = collect_instances()
LEAF_NAMES = sorted({name for name, _ in leaf_instances})
print(f"Instances: {len(leaf_instances)} leaf clusters, {len(twig_instances)} branch pieces")

bake_leaf_atlas(output_path("island_tree_leaves.png"), output_path("island_tree_dry_leaves.png"))
_, facing, occlusion = crown_shading(leaf_instances)

all_clusters = set(range(len(leaf_instances)))
dry_rng = random.Random(SEED + 100)
dry_clusters = {i for i in all_clusters if dry_rng.random() < DRY_LEAF_SHARE}

objects = build_variant(
    "island_tree", output_path("island_tree_leaves.png"), output_path("island_tree_impostor.png"),
    all_clusters, TINT_VARIATION)
objects += build_variant(
    "island_tree_dry", output_path("island_tree_dry_leaves.png"), output_path("island_tree_dry_impostor.png"),
    dry_clusters, DRY_TINT_VARIATION)

export_fbx(objects, output_path("island_tree_reduced.fbx"))
print("Island tree written to", OUTPUT_FOLDER)
