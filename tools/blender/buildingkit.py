"""Shared pieces for the building builders in blender/structures/ (notes B1, B3, B6, W4).

A wall is a rectangle with rectangular openings cut out of it. `solids` gives the solid rectangles that are left, so
a door or a window is a real hole through the wall and the piers and lintels around it are real boxes. `panels` then
splits each of those into wall-face panels, which is how the render coming off an adobe wall in patches is drawn:
every panel is flush with its neighbours, so the wall stays one flat surface and only its material changes.

Collision is the `solids` rectangles, one box each, not the panels: a wall is one slab per pier and lintel. The
panels sit exactly in that slab's faces, so collision still follows the visual to 0 mm, with a tenth of the shapes.
"""
import math
import random

TOL = 1e-9


def solids(length, height, openings):
    """The solid rectangles of a wall `length` x `height` m with `openings` cut out, as (u0, u1, v0, v1) m: u along
    the wall from its centre, v up from its foot. Full-height piers between the openings, plus the band under and
    the lintel band over each one. The openings must not overlap in u, which is true of a wall of a house."""
    rects, u = [], -length / 2
    for u0, u1, v0, v1 in sorted(openings, key=lambda o: o[0]):
        if u0 > u + TOL:
            rects.append((u, u0, 0.0, height))
        if v0 > TOL:
            rects.append((u0, u1, 0.0, v0))
        if v1 < height - TOL:
            rects.append((u0, u1, v1, height))
        u = u1
    if u < length / 2 - TOL:
        rects.append((u, length / 2, 0.0, height))
    return rects


def panels(rect, panel):
    """A solid rectangle split into a grid of panels of about `panel` = (u, v) metres, as (u0, u1, v0, v1). The grid
    divides the rectangle exactly, so the panels tile it with no seam and no overlap."""
    u0, u1, v0, v1 = rect
    columns = max(1, round((u1 - u0) / panel[0]))
    rows = max(1, round((v1 - v0) / panel[1]))
    out = []
    for i in range(columns):
        for j in range(rows):
            out.append((u0 + (u1 - u0) * i / columns, u0 + (u1 - u0) * (i + 1) / columns,
                        v0 + (v1 - v0) * j / rows, v0 + (v1 - v0) * (j + 1) / rows))
    return out


def patchiness(seed, scale=(3.4, 2.1), octaves=3):
    """A smooth seeded field f(u, v) in about [-1, 1], for deciding what a wall panel is made of. Smooth, so the
    render comes off in patches a metre or two across (notes B1) instead of panel-by-panel noise."""
    rng = random.Random(seed)
    waves = [(rng.uniform(0, 2 * math.pi), rng.uniform(0, 2 * math.pi), 1.0 / (k + 1)) for k in range(octaves)]
    weight = sum(w[2] for w in waves)

    def value(u, v):
        total = 0.0
        for k, (pu, pv, amp) in enumerate(waves):
            total += amp * math.sin(2 * math.pi * (k + 1) * u / scale[0] + pu) \
                * math.cos(2 * math.pi * (k + 1) * v / scale[1] + pv)
        return total / weight
    return value


def wall(mesh, shapes, axis, face, centre, length, thickness, height, openings, panel, material, base=0.0,
         uv_seed=0.0):
    """Draws one axis-aligned wall and appends its collision boxes to `shapes`.

    `axis` is "x" for a wall running east-west (its faces look along Z) or "z" for one running north-south. `face`
    is the wall's centre-line coordinate across its thickness, `centre` its centre-line coordinate along `axis`,
    and `base` the height of its foot. `material(u, v)` names the panel's material at the panel's centre, so a
    caller can vary render and bare clay over the wall. Returns the solid rectangles, so a caller can draw trim.
    """
    rects = solids(length, height, openings)
    for rect in rects:
        u0, u1, v0, v1 = rect
        if axis == "x":
            shapes.append({"shape": "box", "size_m": [round(u1 - u0, 4), round(v1 - v0, 4), round(thickness, 4)],
                           "position_m": [round(centre + (u0 + u1) / 2, 4), round(base + (v0 + v1) / 2, 4),
                                          round(face, 4)]})
        else:
            shapes.append({"shape": "box", "size_m": [round(thickness, 4), round(v1 - v0, 4), round(u1 - u0, 4)],
                           "position_m": [round(face, 4), round(base + (v0 + v1) / 2, 4),
                                          round(centre + (u0 + u1) / 2, 4)]})
        for a0, a1, b0, b1 in panels(rect, panel):
            size = (a1 - a0, b1 - b0, thickness)
            at = (centre + (a0 + a1) / 2, base + (b0 + b1) / 2, face)
            mesh.box((at[0], at[1], at[2]) if axis == "x" else (at[2], at[1], at[0]),
                     size if axis == "x" else (size[2], size[1], size[0]),
                     material((a0 + a1) / 2, (b0 + b1) / 2), uv_offset=(uv_seed + a0, b0))
    return rects


def heap(mesh, shapes, rng, at, spread, count, size, material):
    """A low heap of small rigid pieces: tile shards or brick bits at a wall foot (notes T6, S3). Each piece is a
    box with its own collision, because a shard under a leg is what tilts a drone at lift-off."""
    for _ in range(count):
        dx = rng.gauss(0, spread[0])
        dz = rng.gauss(0, spread[1])
        piece = [s * rng.uniform(0.7, 1.35) for s in size]
        shapes.append(mesh.box((at[0] + dx, at[1] + piece[1] / 2, at[2] + dz), piece, material,
                               rotation_deg=(rng.uniform(0, 360), rng.uniform(-22, 22), rng.uniform(-14, 14)),
                               uv_offset=(rng.uniform(0, 2), rng.uniform(0, 2))))


def silhouette_porosity(boxes, u_range, v_range, step=0.05):
    """The open fraction of `boxes` seen along +Z, over the rectangle `u_range` x `v_range` in (x, y) metres.

    A box turned about X only (a rafter, a batten, a tile patch) projects along Z to exactly its axis-aligned
    extent, so this is exact for a roof; a box turned about Y or Z is counted by that extent, which over-states its
    cover a little. Rasterised, so a whole roof costs one pass per box.
    """
    u0, u1 = u_range
    v0, v1 = v_range
    columns = max(1, round((u1 - u0) / step))
    rows = max(1, round((v1 - v0) / step))
    filled = bytearray(columns * rows)
    for box in boxes:
        half = _extent(box)
        c = box["position_m"]
        for j in range(max(0, math.floor((c[1] - half[1] - v0) / step)),
                       min(rows, math.ceil((c[1] + half[1] - v0) / step))):
            for i in range(max(0, math.floor((c[0] - half[0] - u0) / step)),
                           min(columns, math.ceil((c[0] + half[0] - u0) / step))):
                filled[j * columns + i] = 1
    return round(1 - sum(filled) / (columns * rows), 2)


def _extent(box):
    """Half the box's axis-aligned extent, its rotation included."""
    size = box["size_m"]
    yaw, pitch, roll = box.get("rotation_deg", (0.0, 0.0, 0.0))
    rows = _matrix(yaw, pitch, roll)
    return [sum(abs(rows[r][k]) * size[k] / 2 for k in range(3)) for r in range(3)]


def _matrix(yaw, pitch, roll):
    cy, sy = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
    cx, sx = math.cos(math.radians(pitch)), math.sin(math.radians(pitch))
    cz, sz = math.cos(math.radians(roll)), math.sin(math.radians(roll))
    ry = ((cy, 0, sy), (0, 1, 0), (-sy, 0, cy))
    rx = ((1, 0, 0), (0, cx, -sx), (0, sx, cx))
    rz = ((cz, -sz, 0), (sz, cz, 0), (0, 0, 1))
    mul = lambda a, b: tuple(tuple(sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)) for i in range(3))
    return mul(mul(ry, rx), rz)
