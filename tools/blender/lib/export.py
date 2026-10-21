"""The Blender side of the build: scene objects from lib.geometry meshes, and a reproducible FBX export."""
import datetime
import hashlib
import types

import bpy

from . import frame

FIXED_CLOCK = datetime.datetime(2000, 1, 1)
# Design decision 5: Unity-friendly axes with the transform and the units baked into the vertices (lib.frame).
# Static meshes only; normals per corner (flat but a mesh's `smooth` slots and its faces' own normals); triangles,
# so the file holds exactly what Unity renders.
FBX_OPTIONS = dict(
    axis_forward='-Z', axis_up='Y', bake_space_transform=True, apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_NONE', global_scale=1.0, object_types={'MESH'}, mesh_smooth_type='OFF',
    use_triangles=True, use_tspace=False, colors_type='NONE', use_custom_props=False, add_leaf_bones=False,
    bake_anim=False, embed_textures=False, path_mode='STRIP', use_metadata=True)


def reset_scene():
    """An empty factory scene, so a build depends on nothing but its params and generator."""
    bpy.ops.wm.read_factory_settings(use_empty=True)


def add_lod(name, mesh):
    """One mesh object `name` from a lib.geometry.Mesh: vertices in Blender's frame, every face wound
    counter-clockwise about its outward normal, a UV map in metres, one material per slot named by the slot, the
    faces of the mesh's `smooth` slots shaded smooth, and the corners of faces with normals of their own
    (`corner_normals`) given them as custom split normals (zero, Blender's own, elsewhere). Those faces are shaded
    smooth too: the exporter triangulates, which keeps a smooth polygon's custom normals and scrambles a flat one's,
    since Blender stores them relative to each corner's own frame (a flat 12-gon's normal leaning up 11.9 degrees came
    out of the triangulation still 11.9 degrees off the face normal but turned about it, many corners leaning
    sideways; p1-v01-locust task 1.2j). When the mesh's vertices carry them (p1-l01-l03-pole-wires): a second UV map
    of their second UVs ((0, 0) elsewhere), which Unity reads as uv2, and their normals as custom normals on the faces
    whose every vertex has one, those faces smooth-shaded; every other face keeps its flat normal. A mesh with none of
    these exports as before. A mesh whose vertices carry normals and that also has `corner_normals` or `smooth` slots
    is refused: the vertices' block would replace the corners' normals and reset every face's smoothing (QA 2.1, S7)."""
    if mesh.normals and (mesh.corner_normals or mesh.smooth):
        mixed = "per-corner normals" if mesh.corner_normals else f"smooth slots {sorted(mesh.smooth)}"
        raise ValueError(f"{name}: a mesh takes per-vertex normals or {mixed}, not both")
    verts = [frame.unity_to_blender(v) for v in mesh.vertices]
    loops, uvs, corners, slots = [], [], [], mesh.slots()
    for f, (indices, outward, face_uvs, _) in enumerate(mesh.faces):
        corner_normals = [frame.unity_to_blender(n) for n in mesh.corner_normals.get(f, [(0.0, 0.0, 0.0)] * len(indices))]
        if _dot(_newell(verts, indices), frame.unity_to_blender(outward)) < 0:
            indices, face_uvs, corner_normals = indices[::-1], face_uvs[::-1], corner_normals[::-1]
        loops.append(indices)
        uvs.append(face_uvs)
        corners += corner_normals
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], loops)
    if me.validate(verbose=True):
        raise RuntimeError(f"{name}: Blender had to correct the geometry; the generator produced an invalid mesh")
    for slot in slots:
        me.materials.append(bpy.data.materials.get(slot) or bpy.data.materials.new(slot))
    if mesh.smooth or mesh.corner_normals:  # a mesh with neither stays exactly as before
        me.polygons.foreach_set("use_smooth", [slot in mesh.smooth or f in mesh.corner_normals
                                               for f, (_, _, _, slot) in enumerate(mesh.faces)])
    if mesh.corner_normals:  # likewise
        me.normals_split_custom_set(corners)
    layer = me.uv_layers.new(name="UVMap")
    for poly, face_uvs, (_, _, _, slot) in zip(me.polygons, uvs, mesh.faces):
        poly.material_index = slots.index(slot)
        for loop_index, uv in zip(poly.loop_indices, face_uvs):
            layer.data[loop_index].uv = uv
    if mesh.uv2:
        second = me.uv_layers.new(name="UVMap2")
        for loop in me.loops:
            second.data[loop.index].uv = mesh.uv2.get(loop.vertex_index, (0.0, 0.0))
    if mesh.normals:
        normals = {i: frame.unity_to_blender(n) for i, n in mesh.normals.items()}
        smooth = [all(v in normals for v in poly.vertices) for poly in me.polygons]
        for poly, flag in zip(me.polygons, smooth):
            poly.use_smooth = flag
        corner = [(0.0, 0.0, 0.0)] * len(me.loops)  # zero: keep the face's own normal
        for poly, flag in zip(me.polygons, smooth):
            if flag:
                for loop_index in poly.loop_indices:
                    corner[loop_index] = normals[me.loops[loop_index].vertex_index]
        me.normals_split_custom_set(corner)
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def export_fbx(path):
    """Writes the scene's meshes to `path` so that unchanged input gives identical bytes (design: risks). The
    add-on's two varying inputs are pinned: its clock, and its object ids, which it derives from Python's
    per-process-salted hash() of a key string. Its FileId is a constant already. Both seams are the add-on's own
    private names, held by the Blender pin in build.py."""
    import io_scene_fbx.export_fbx_bin as exporter
    import io_scene_fbx.fbx_utils as utils
    exporter.datetime = types.SimpleNamespace(datetime=types.SimpleNamespace(now=lambda: FIXED_CLOCK))
    utils._key_to_uuid = _stable_key_to_uuid
    bpy.ops.export_scene.fbx(filepath=path, **FBX_OPTIONS)


def _stable_key_to_uuid(uuids, key):
    """io_scene_fbx.fbx_utils._key_to_uuid with hash(key) replaced by SHA-1 of repr(key); same clamping after."""
    import io_scene_fbx.fbx_utils as utils
    if isinstance(key, int) and 0 <= key < 2**63:
        uuid = key
    else:
        uuid = int.from_bytes(hashlib.sha1(repr(key).encode("utf-8")).digest()[:8], "little") >> 1
    if uuid > int(1e9):
        shorter = uuid % int(1e9)
        if shorter not in uuids:
            uuid = shorter
    if uuid in uuids:
        step = 1 if uuid < 2**62 else -1
        while uuid in uuids:
            uuid += step
    return utils.UUID(uuid)


def _newell(verts, indices):
    """The polygon normal of a counter-clockwise loop (Newell's method), unnormalised."""
    n = [0.0, 0.0, 0.0]
    for i, j in zip(indices, indices[1:] + indices[:1]):
        a, b = verts[i], verts[j]
        n[0] += (a[1] - b[1]) * (a[2] + b[2])
        n[1] += (a[2] - b[2]) * (a[0] + b[0])
        n[2] += (a[0] - b[0]) * (a[1] + b[1])
    return n


def _dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
