"""Modelling kit for asset builder scripts, blender/<class>/<asset>.py (see tools/blender/README.md).

A builder draws in catalog asset space: metres, +X east, +Y up, +Z south, origin on the ground at the footprint
centre, rotations as the catalog's rotation_deg. Each box it draws comes back as the catalog collision shape of that
same box, so collision follows the visual by construction. The kit converts to Blender's Z-up frame, (x, y, z) ->
(x, -z, y), and the glTF exporter converts back.
"""
import json
import math
import os

import bpy


def reset():
    """Starts from an empty scene with factory settings, so a build depends on nothing but its script."""
    bpy.ops.wm.read_factory_settings(use_empty=True)


def rotation(yaw, pitch, roll):
    """The catalog's rotation: Godot's YXZ Euler basis Ry·Rx·Rz, in degrees, as rows of a 3×3 matrix."""
    cy, sy = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
    cx, sx = math.cos(math.radians(pitch)), math.sin(math.radians(pitch))
    cz, sz = math.cos(math.radians(roll)), math.sin(math.radians(roll))
    ry = ((cy, 0, sy), (0, 1, 0), (-sy, 0, cy))
    rx = ((1, 0, 0), (0, cx, -sx), (0, sx, cx))
    rz = ((cz, -sz, 0), (sz, cz, 0), (0, 0, 1))
    mul = lambda a, b: tuple(tuple(sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)) for i in range(3))
    return mul(mul(ry, rx), rz)


class Mesh:
    """The geometry of one LOD: boxes, each with a material, UV-mapped in metres."""

    def __init__(self):
        self.verts, self.faces, self.uvs, self.face_materials = [], [], [], []

    def box(self, center, size, material, rotation_deg=(0, 0, 0), uv_offset=(0.0, 0.0)):
        """Adds a box and returns it as a catalog collision shape. Values are rounded to 0.1 mm and 0.001°, and the
        mesh uses the rounded values, so the shape and the mesh agree exactly. The box's longest side runs along V
        on every face, so wood grain follows a plank."""
        center = [round(v, 4) for v in center]
        size = [round(v, 4) for v in size]
        rotation_deg = [round(v, 3) for v in rotation_deg]
        r = rotation(*rotation_deg)
        half = [s / 2 for s in size]
        base = len(self.verts)
        for i in range(8):
            local = [half[a] if i >> a & 1 else -half[a] for a in range(3)]
            p = [center[row] + sum(r[row][k] * local[k] for k in range(3)) for row in range(3)]
            self.verts.append((p[0], -p[2], p[1]))
        for axis in range(3):
            u_axis, v_axis = [a for a in range(3) if a != axis]
            if size[u_axis] > size[v_axis]:
                u_axis, v_axis = v_axis, u_axis
            for side in (0, 1):
                corners = [i for i in range(8) if (i >> axis & 1) == side]
                # Order the 4 corners around the face, counter-clockwise seen from outside.
                ring = sorted(corners, key=lambda i: math.atan2((i >> v_axis & 1) - 0.5, (i >> u_axis & 1) - 0.5))
                if (u_axis + 1) % 3 != v_axis:
                    ring.reverse()
                if side == 0:
                    ring.reverse()
                self.faces.append([base + i for i in ring])
                self.uvs.append([((i >> u_axis & 1) * size[u_axis] + uv_offset[0],
                                  (i >> v_axis & 1) * size[v_axis] + uv_offset[1]) for i in ring])
                self.face_materials.append(material)
        shape = {"shape": "box", "size_m": size, "position_m": center}
        if any(rotation_deg):
            shape["rotation_deg"] = rotation_deg
        return shape


def finish(builder_file, budget_class, lods, materials, entry):
    """Makes one object per LOD, <asset>_LOD<n>, stores the budget class and the catalog entry in the scene, and saves
    the .blend next to the builder. `materials` maps each material name to (albedo "#rrggbb", roughness): the flat
    look until the game binds game/assets/materials/<name>.tres."""
    asset = os.path.splitext(os.path.basename(builder_file))[0]
    made = {}
    for name, (albedo, roughness) in sorted(materials.items()):
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        m.use_backface_culling = True  # closed meshes: exported single-sided, so Godot culls back faces
        shader = m.node_tree.nodes["Principled BSDF"]
        srgb = [int(albedo[i:i + 2], 16) / 255 for i in (1, 3, 5)]
        linear = [c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in srgb]
        shader.inputs["Base Color"].default_value = (*linear, 1.0)
        shader.inputs["Roughness"].default_value = roughness
        made[name] = m
    for n, lod in enumerate(lods):
        mesh = bpy.data.meshes.new(f"{asset}_LOD{n}")
        mesh.from_pydata(lod.verts, [], lod.faces)
        used = sorted(set(lod.face_materials))
        for name in used:
            mesh.materials.append(made[name])
        uv = mesh.uv_layers.new(name="UVMap")
        for poly, face_uvs, material in zip(mesh.polygons, lod.uvs, lod.face_materials):
            poly.material_index = used.index(material)
            for loop, coord in zip(poly.loop_indices, face_uvs):
                uv.data[loop].uv = coord
        mesh.validate()
        bpy.context.scene.collection.objects.link(bpy.data.objects.new(mesh.name, mesh))
    bpy.context.scene["budget_class"] = budget_class
    bpy.context.scene["catalog_entry"] = json.dumps(entry)
    path = os.path.splitext(os.path.abspath(builder_file))[0] + ".blend"
    bpy.ops.wm.save_as_mainfile(filepath=path, compress=True)
    print(f"built {path}")
