"""Lofted sheets (p1-r01-vaz-sedan, design decision 1): structured grids of points lofted through a body's
sections, emitted into a lib.geometry.Mesh as a single surface or as a sheet of real thickness with its edge faces.
Plain Python in asset space (lib.frame); no Blender.

A grid is a list of rows, each a list of points of the same length: rows run along the loft (one per section), a
row's points across the section. Quad (i, j) has the corners grid[i][j], grid[i][j + 1], grid[i + 1][j + 1] and
grid[i + 1][j]. A mask (the same shape as the quads, True where a quad exists) cuts openings: a window, a door's
hole in the body side, a glass pane in the roof cap. Corners that coincide (a row clamped up to a wheel arch's edge)
are one vertex, so a quad that loses a corner is a triangle and one that loses two is dropped.

`sheet` with a thickness builds the double-sided sheet the asset rules ask for (REAL-05): the outer surface, the
inner surface offset inward along the vertex normals by the thickness, and an edge strip of that width along every
border, the grid's own and every opening's, so each panel shows its gauge at every opening and cut. `outward` is a
function of a point returning a direction the outer surface's normals must agree with (a body's section: away from
its axis); the inner surface faces the other way. UVs are given per grid point in metres (AST-07: continuous over a
panel, upright on its sides); the inner surface takes the outer's, an edge strip its border's and the thickness.
"""
import math

MERGE_M = 1e-7  # grid corners closer than this are one vertex


def interp(table, t):
    """Piecewise-linear value of a sorted [(t, value), ...] table at t, held constant beyond its ends."""
    if t <= table[0][0]:
        return table[0][1]
    for (t0, v0), (t1, v1) in zip(table, table[1:]):
        if t <= t1:
            return v0 if t1 == t0 else v0 + (v1 - v0) * (t - t0) / (t1 - t0)
    return table[-1][1]


def samples(lo, hi, step, extra=()):
    """Sorted parameters from lo to hi at most `step` apart, through every value of `extra` inside [lo, hi]."""
    fixed = sorted({lo, hi} | {e for e in extra if lo < e < hi})
    out = [fixed[0]]
    for a, b in zip(fixed, fixed[1:]):
        n = max(1, math.ceil((b - a) / step - 1e-9))
        out += [a + (b - a) * k / n for k in range(1, n + 1)]
    return out


def sheet(mesh, grid, uvs, slot, outward, thickness=0.0, mask=None, edge_slot=None, slots=None, seams=()):
    """Emits the grid's quads into mesh (see the module docstring). `slots`, if given, names a slot per quad
    (overriding `slot`); `edge_slot` the edge strips' slot (default: the quad's). `seams` names masked quads that
    another surface fills (a hole's patch, lib.kits_vehicle): no edge strip is drawn against them. Returns the
    number of quads emitted on the outer surface."""
    rows, cols = len(grid), len(grid[0])
    quads = [(i, j) for i in range(rows - 1) for j in range(cols - 1) if mask is None or mask[i][j]]
    if not quads:
        return 0
    normals = _vertex_normals(grid, quads, outward)
    outer = _indexer(mesh, grid)
    inner = None
    if thickness > 0:
        offset = [[tuple(grid[i][j][a] - thickness * normals[i][j][a] for a in range(3)) for j in range(cols)]
                  for i in range(rows)]
        inner = _indexer(mesh, offset)
    emitted = 0
    for i, j in quads:
        quad_slot = slots[i][j] if slots else slot
        corners = [(i, j), (i, j + 1), (i + 1, j + 1), (i + 1, j)]
        normal = _face(mesh, outer, grid, uvs, corners, outward, quad_slot)
        if normal is None:
            continue
        emitted += 1
        if inner is not None:
            _face(mesh, inner, grid, uvs, corners, lambda p, n=normal: (-n[0], -n[1], -n[2]), quad_slot)
    if inner is not None:
        present = set(quads) | set(seams)
        for i, j in quads:
            for a, b, nb in (((i, j), (i, j + 1), (i - 1, j)), ((i, j + 1), (i + 1, j + 1), (i, j + 1)),
                             ((i + 1, j + 1), (i + 1, j), (i + 1, j)), ((i + 1, j), (i, j), (i, j - 1))):
                if nb in present:
                    continue
                centre = _centroid([grid[r][c] for r, c in ((i, j), (i, j + 1), (i + 1, j + 1), (i + 1, j))])
                _edge(mesh, outer, inner, grid, uvs, a, b, centre, thickness,
                      edge_slot or (slots[i][j] if slots else slot))
    return emitted


def surface(mesh, grid, uvs, slot, outward, mask=None, slots=None):
    """A single-sided surface (no thickness, no edges): a liner, an underbody, a backing plane."""
    return sheet(mesh, grid, uvs, slot, outward, 0.0, mask, None, slots)


def _indexer(mesh, grid):
    """Lazily adds each grid point as a mesh vertex, merging points closer than MERGE_M (per call)."""
    cache, by_position = {}, {}

    def index(i, j):
        if (i, j) not in cache:
            p = grid[i][j]
            key = _key(p)
            if key not in by_position:
                by_position[key] = mesh.vertex(p)
            cache[i, j] = by_position[key]
        return cache[i, j]
    return index


def _key(p):
    return tuple(round(c / MERGE_M) for c in p)


def _face(mesh, index, grid, uvs, corners, outward, slot):
    """One quad (or the triangle left when two corners merge), its normal agreeing with outward; the normal, or
    None when it is degenerate."""
    keys, kept, pts, tex = [], [], [], []
    for r, c in corners:
        key = _key(grid[r][c])
        if key in keys:
            continue
        keys.append(key)
        kept.append((r, c))
        pts.append(grid[r][c])
        tex.append(uvs[r][c])
    if len(kept) < 3:
        return None
    n = _newell(pts)
    length = math.sqrt(_dot(n, n))
    if length < 1e-12:
        return None
    n = tuple(c / length for c in n)
    if _dot(n, outward(_centroid(pts))) < 0:
        n = (-n[0], -n[1], -n[2])
    mesh.face([index(r, c) for r, c in kept], n, slot, tex)
    return n


def _edge(mesh, outer, inner, grid, uvs, a, b, centre, thickness, slot):
    """The strip along one border segment a-b between the outer and inner surfaces, facing away from centre."""
    pa, pb = grid[a[0]][a[1]], grid[b[0]][b[1]]
    ids = [outer(*a), outer(*b), inner(*b), inner(*a)]
    if len(set(ids)) < 4 or math.dist(pa, pb) < MERGE_M:
        return
    mid = _centroid([pa, pb])
    away = tuple(mid[k] - centre[k] for k in range(3))
    n = _newell([pa, pb, mesh.vertices[ids[2]], mesh.vertices[ids[3]]])
    length = math.sqrt(_dot(n, n))
    if length < 1e-14:
        return
    n = tuple(c / length for c in n)
    if _dot(n, away) < 0:
        n = (-n[0], -n[1], -n[2])
    ua, ub = uvs[a[0]][a[1]], uvs[b[0]][b[1]]
    mesh.face(ids, n, slot, [ua, ub, (ub[0], ub[1] - thickness), (ua[0], ua[1] - thickness)])


def _vertex_normals(grid, quads, outward):
    """Per grid point, the normalised sum of the outward normals of the quads that use it."""
    rows, cols = len(grid), len(grid[0])
    acc = [[[0.0, 0.0, 0.0] for _ in range(cols)] for _ in range(rows)]
    for i, j in quads:
        corners = [(i, j), (i, j + 1), (i + 1, j + 1), (i + 1, j)]
        pts = [grid[r][c] for r, c in corners]
        n = _newell(pts)
        if _dot(n, outward(_centroid(pts))) < 0:
            n = (-n[0], -n[1], -n[2])
        for r, c in corners:
            for k in range(3):
                acc[r][c][k] += n[k]
    out = []
    for i in range(rows):
        row = []
        for j in range(cols):
            v = acc[i][j]
            length = math.sqrt(_dot(v, v))
            if length < 1e-15:
                v = outward(grid[i][j])
                length = math.sqrt(_dot(v, v)) or 1.0
            row.append(tuple(c / length for c in v))
        out.append(row)
    return out


def _newell(pts):
    n = [0.0, 0.0, 0.0]
    for a, b in zip(pts, pts[1:] + pts[:1]):
        n[0] += (a[1] - b[1]) * (a[2] + b[2])
        n[1] += (a[2] - b[2]) * (a[0] + b[0])
        n[2] += (a[0] - b[0]) * (a[1] + b[1])
    return n


def _centroid(pts):
    return tuple(sum(p[k] for p in pts) / len(pts) for k in range(3))


def _dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
