"""The Blender side of the build: scene objects from lib.geometry meshes, and a reproducible FBX export."""
import datetime
import hashlib
import types

import bpy

from . import frame

FIXED_CLOCK = datetime.datetime(2000, 1, 1)
# Design decision 5: Unity-friendly axes with the transform and the units baked into the vertices (lib.frame).
# Static meshes only; normals per corner (flat unless a generator smooths); triangles, so the file holds exactly
# what Unity renders.
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
    counter-clockwise about its outward normal, a UV map in metres, and one material per slot named by the slot."""
    verts = [frame.unity_to_blender(v) for v in mesh.vertices]
    loops, uvs, slots = [], [], mesh.slots()
    for indices, outward, face_uvs, _ in mesh.faces:
        if _dot(_newell(verts, indices), frame.unity_to_blender(outward)) < 0:
            indices, face_uvs = indices[::-1], face_uvs[::-1]
        loops.append(indices)
        uvs.append(face_uvs)
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], loops)
    if me.validate(verbose=True):
        raise RuntimeError(f"{name}: Blender had to correct the geometry; the generator produced an invalid mesh")
    for slot in slots:
        me.materials.append(bpy.data.materials.get(slot) or bpy.data.materials.new(slot))
    layer = me.uv_layers.new(name="UVMap")
    for poly, face_uvs, (_, _, _, slot) in zip(me.polygons, uvs, mesh.faces):
        poly.material_index = slots.index(slot)
        for loop_index, uv in zip(poly.loop_indices, face_uvs):
            layer.data[loop_index].uv = uv
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
