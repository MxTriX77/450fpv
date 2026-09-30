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


def _add(a, b):
    return (a[0] + b[0], a[1] + b[1], a[2] + b[2])


def _sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def _scale(v, k):
    return (v[0] * k, v[1] * k, v[2] * k)


def _cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def _normalize(v):
    length = math.sqrt(sum(c * c for c in v))
    return (0.0, 1.0, 0.0) if length < 1e-9 else _scale(v, 1 / length)


def _frame(direction):
    """Two unit vectors across `direction`, picked so a nearly vertical branch gets a stable ring."""
    up = (1.0, 0.0, 0.0) if abs(direction[1]) > 0.95 else (0.0, 1.0, 0.0)
    x = _normalize(_cross(up, direction))
    return x, _normalize(_cross(direction, x))


class Mesh:
    """The geometry of one LOD: boxes, tubes and cards, each with a material, UV-mapped in metres."""

    def __init__(self):
        self.verts, self.faces, self.uvs, self.face_materials = [], [], [], []
        self.normals = {}  # face index -> one normal per corner, for the faces that set their own (leaf cards)

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

    def tube(self, points, radii, sides, material):
        """A round tapered limb through `points` (asset space) with a radius at each: trunks, branches and twigs.
        A final radius of 0 closes the tip to a point, so the limb needs no cap. The rings are carried along the
        polyline without twisting, and the UV runs in metres, around the girth in U and along the limb in V, so the
        bark set keeps one scale from the trunk to the last twig. Collision is the builder's business: a limb the
        drone can hit gets a cylinder or capsule shape sized to these same numbers."""
        ring_of, along = [], 0.0
        x_axis, y_axis = _frame(_normalize((points[1][0] - points[0][0], points[1][1] - points[0][1],
                                            points[1][2] - points[0][2])))
        for i, (point, radius) in enumerate(zip(points, radii)):
            if i:
                step = (points[i][0] - points[i - 1][0], points[i][1] - points[i - 1][1], points[i][2] - points[i - 1][2])
                direction = _normalize(step)
                along += math.sqrt(sum(c * c for c in step))
                # Carry the frame along the bend instead of rebuilding it, so the tube never twists at a kink.
                y_axis = _normalize(_cross(direction, x_axis))
                x_axis = _normalize(_cross(y_axis, direction))
            if radius <= 0:
                ring_of.append(([self._vertex(point)] * sides, along))
                continue
            ring = []
            for k in range(sides):
                angle = 2 * math.pi * k / sides
                offset = _add(_scale(x_axis, math.cos(angle) * radius), _scale(y_axis, math.sin(angle) * radius))
                ring.append(self._vertex(_add(point, offset)))
            ring_of.append((ring, along))
        for (lower, v0), (upper, v1) in zip(ring_of, ring_of[1:]):
            girth = 2 * math.pi * max(radii)
            for k in range(sides):
                n = (k + 1) % sides
                corners = [lower[k], lower[n], upper[n], upper[k]]
                uvs = [(girth * k / sides, v0), (girth * (k + 1) / sides, v0),
                       (girth * (k + 1) / sides, v1), (girth * k / sides, v1)]
                if corners[0] == corners[1]:      # a closed lower end: one triangle up to the ring
                    corners, uvs = corners[1:], uvs[1:]
                elif corners[2] == corners[3]:    # a closed tip
                    corners, uvs = corners[:3], uvs[:3]
                self.faces.append(corners)
                self.uvs.append(uvs)
                self.face_materials.append(material)

    def card(self, center, across, up, material, tile, normal=None):
        """One flat quad: a leaf cluster, a twig fan or a far impostor, cut out of the foliage atlas. `across` and `up`
        are the half-edges from the centre, so their lengths set the card's size and their directions its facing.
        `tile` is the atlas tile (column, row) counted from the image's top-left, as tools/assets/make_foliage.py
        lays it out. `normal` replaces the quad's own flat normal on all four corners: a crown whose cards all point
        away from its centre is lit as one rounded mass instead of a heap of separate plates."""
        corners = [_add(center, _add(_scale(across, sx), _scale(up, sy)))
                   for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        u0, v0 = tile[0] * 0.5, tile[1] * 0.5
        # Blender's V runs up from the image's bottom and the glTF exporter flips it back, so V is mirrored here.
        self.faces.append([self._vertex(c) for c in corners])
        self.uvs.append([(u0, 1 - v0 - 0.5), (u0 + 0.5, 1 - v0 - 0.5), (u0 + 0.5, 1 - v0), (u0, 1 - v0)])
        self.face_materials.append(material)
        if normal is not None:
            n = _normalize(normal)
            self.normals[len(self.faces) - 1] = [(n[0], -n[2], n[1])] * 4

    def _vertex(self, point):
        """Adds one vertex in Blender's Z-up frame, rounded to 0.1 mm, and gives its index."""
        self.verts.append((round(point[0], 4), round(-point[2], 4), round(point[1], 4)))
        return len(self.verts) - 1


def finish(builder_file, budget_class, lods, materials, entry):
    """Makes one object per LOD, <asset>_LOD<n>, stores the budget class and the catalog entry in the scene, and saves
    the .blend next to the builder. `materials` maps each material name to (albedo "#rrggbb", roughness), or to
    (albedo, roughness, "cutout") for a two-sided alpha-cutout material such as foliage: the flat look until the game
    binds game/assets/materials/<name>.tres."""
    asset = os.path.splitext(os.path.basename(builder_file))[0]
    made = {}
    for name, settings in sorted(materials.items()):
        albedo, roughness = settings[0], settings[1]
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        # Closed meshes are exported single-sided, so Godot culls back faces; a cutout card is one face seen from both.
        m.use_backface_culling = len(settings) < 3 or settings[2] != "cutout"
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
        if lod.normals:
            corner = [tuple(v.vector) for v in mesh.corner_normals]
            for face, normals in lod.normals.items():
                for k, loop in enumerate(mesh.polygons[face].loop_indices):
                    corner[loop] = normals[k]
            mesh.normals_split_custom_set(corner)
        bpy.context.scene.collection.objects.link(bpy.data.objects.new(mesh.name, mesh))
    bpy.context.scene["budget_class"] = budget_class
    bpy.context.scene["catalog_entry"] = json.dumps(entry)
    path = os.path.splitext(os.path.abspath(builder_file))[0] + ".blend"
    bpy.ops.wm.save_as_mainfile(filepath=path, compress=True)
    print(f"built {path}")
