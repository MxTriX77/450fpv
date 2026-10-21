"""The K02 clay-tile pieces as library builders (p1-h01-adobe-house, design decision 10; object sheet
docs/object-sheets/lines-roads-debris.md §K02): a shard broken from a tile and a whole tile, for the `debris_tile`
generator and for a host's rubble (the `house` generator's tile heap). Plain Python on lib.geometry, no Blender.

A piece is a dict {"lods": [LOD0, LOD1], "collision", "snag", "materials": [{"slot", "physics"}]} in its own frame:
x along the tile (tail -x, head +x), z across it, y up, its box centred on the origin in plan and its lowest point on
y = 0. A host places it with lib.kits_vehicle's Rigid (append_mesh, transform_shape, transform_snag), keeping the
slot names, so its catalog's visuals for `tile` and `tile_fracture` colour the pieces as its roof.

The tile (`Tile`): length along x, width across, thickness, and a camber along its length, the middle of the tile
standing `camber` above its two ends (the top at c(x) + thickness, c(x) = camber (1 - (2x / length)^2)). A whole tile
carries its nib, a lug under the head (NIB_M) that hangs it on its batten.

The shard (`shard_outline`): a tile broken by `breaks` fracture lines is a near-rectangle of the tile (its box, the
collision box) whose sides are the tile's own straight edges or jagged fractures. Two breaks leave a corner piece or a
strip across or along the tile, three an edge piece, four an inner piece, and each break beyond four chips a corner.
A fracture side's two ends stray up to TILT_M off the box side and its inner vertices, every SEG_M, up to JAG_M
more, so the outline stays within FIT_M of its box in plan: one box holds the shard within AST-05's 2 cm zone
anywhere a host puts it (the sheet's one thin box per shard). The fracture rims are their own slot, paler (the sheet's
fracture face), and carry `edge` snags at the top arris, radius EDGE_SNAG_R_M.

UVs (AST-07): the tile's top, the clay surface, maps into one clean tile face of the set (CELLS_PX, measured on
polyhaven/clay_roof_tiles_02's 1k diffuse; V towards the head, as the photo's tiles lie), at the uniform scale that
fits the whole tile, centred; a shard keeps its parent tile's UVs. A rim runs once along the cell's height.
"""
import collections
import math

from lib import kits_vehicle
from lib.geometry import Mesh

TILE_SLOT, FRACTURE_SLOT = "tile", "tile_fracture"
PHYSICS = "masonry"  # the sheet: tiles are masonry (breakability later)
Tile = collections.namedtuple("Tile", "length width thickness camber")

NIB_M = (0.020, 0.040, 0.015)  # along, across, below the head (assumed: one central lug of a pressed tile)
EDGE_SNAG_R_M = 0.002          # the sheet: tile fracture rims, `edge`, 2 mm
TILT_M = 0.008                 # a fracture side's ends off its box side (assumed)
JAG_M = 0.003                  # a fracture's roughness (assumed: fired clay breaks rough by 1-3 mm)
SEG_M = 0.02                   # vertex spacing along a fracture (assumed)
CHIP_M = (0.006, 0.012)        # the legs of a corner chip (assumed)
FIT_M = math.sqrt(2) * TILT_M + JAG_M  # derived: the farthest an outline strays from its box in plan
MARGIN_M = 0.02                # a fracture keeps this far inside the tile's own edges (derived: > FIT_M)
SHORT_SIDE = (0.45, 0.9)       # a shard's short side as a share of its long one (assumed)
CAMBER_SEGMENTS = (8, 2)       # a whole tile's strips along its length per LOD
# Clean tile faces on the 1k diffuse of polyhaven/clay_roof_tiles_02, inclusive pixel boxes (x0, y0), (x1, y1) with
# rows from the top, inset from the joints: five orange and one weathered (measured on the map; task 1.5 took two
# weathered ones out, whose dark faces made a two-tone chequer at 10 m where E's tiles read one orange). The
# clay_tile recipe's tint lands the mean of the six on the sheet's #a0613f.
CELLS_PX = (((48, 14), (80, 86)), ((135, 95), (166, 150)), ((306, 256), (334, 326)), ((180, 8), (210, 78)),
            ((268, 312), (296, 380)), ((390, 322), (424, 400)))
SET_PX, SET_M = 1024, 2.5  # the map's size and its published 2.5 m (recipe clay_tile, tile_size_m)


def _cell_m(cell_px):
    """A pixel box of the set's map as (min, max) UV corners in metres: row 0 is the top of the map, V runs up."""
    (x0, y0), (x1, y1) = cell_px
    return ((x0 / SET_PX * SET_M, (1 - (y1 + 1) / SET_PX) * SET_M),
            ((x1 + 1) / SET_PX * SET_M, (1 - y0 / SET_PX) * SET_M))


CELLS_M = tuple(_cell_m(c) for c in CELLS_PX)


def camber_y(tile, x):
    """The tile's underside at x along it: its ends on 0, its middle `camber` up."""
    return tile.camber * (1 - (2 * x / tile.length) ** 2)


class _CellUV:
    """UVs in one cell: a tile point (x along, z across) at the uniform scale that fits the whole tile, centred, V
    towards the head; a rim point by its distance round the outline, the outline's length spanning the cell's height."""

    def __init__(self, tile, cell):
        (u0, v0), (u1, v1) = CELLS_M[cell]
        self.centre = ((u0 + u1) / 2, (v0 + v1) / 2)
        self.height = v1 - v0
        self.scale = min((u1 - u0) / tile.width, (v1 - v0) / tile.length)

    def top(self, x, z):
        return (self.centre[0] + z * self.scale, self.centre[1] + x * self.scale)

    def rim(self, along, perimeter, y):
        s = self.height / perimeter
        return (self.centre[0] + y * s, self.centre[1] + (along - perimeter / 2) * s)


# --- the shard's outline --------------------------------------------------------------------------------------------

def shard_outline(rng, tile, size, breaks):
    """(points, fracture, corners, box) of a shard of `size` (its long side, m) broken out by `breaks` (2-6): the
    outline in the tile's plan as (x, z), its signed area positive; per edge i (points i to i + 1) whether it is a
    fracture; the indices of its corners (the points LOD1 keeps); and the box (x0, z0, x1, z1) it stays within FIT_M
    of."""
    along = size > tile.width - 2 * MARGIN_M or rng.random() < 0.5
    e_long = min(size, tile.length if along else tile.width)
    e_short = min(size * rng.uniform(*SHORT_SIDE), tile.width if along else tile.length)
    ex, ez = (e_long, e_short) if along else (e_short, e_long)
    factory = max(0, 4 - breaks)
    options = []
    for mx in ("full", "end", "inner"):
        for mz in ("full", "end", "inner"):
            count = {"full": 2, "end": 1, "inner": 0}
            if count[mx] + count[mz] != factory:
                continue
            if (mx == "full" and ex < tile.length - 2 * MARGIN_M) or (mz == "full" and ez < tile.width - 2 * MARGIN_M):
                continue  # a strip only where the shard nearly spans the tile already
            options.append((mx, mz))
    mx, mz = options[rng.randrange(len(options))]
    x0, x1 = _interval(rng, mx, ex, tile.length)
    z0, z1 = _interval(rng, mz, ez, tile.width)
    corners = [(x0, z0), (x1, z0), (x1, z1), (x0, z1)]
    normals = [(0.0, -1.0), (1.0, 0.0), (0.0, 1.0), (-1.0, 0.0)]  # outward, sides z0, x1, z1, x0 in order
    fracture = [abs(z0 + tile.width / 2) > 1e-12, abs(x1 - tile.length / 2) > 1e-12,
                abs(z1 - tile.width / 2) > 1e-12, abs(x0 + tile.length / 2) > 1e-12]
    offsets = [(rng.uniform(-TILT_M, TILT_M), rng.uniform(-TILT_M, TILT_M)) if f else (0.0, 0.0) for f in fracture]
    moved = []
    for k in range(4):  # corner k ends side k - 1 and starts side k
        (ax, az), (bx, bz) = normals[k - 1], normals[k]
        oa, ob = offsets[k - 1][1], offsets[k][0]
        moved.append((corners[k][0] + oa * ax + ob * bx, corners[k][1] + oa * az + ob * bz))
    chips = set(rng.sample(range(4), breaks - 4)) if breaks > 4 else set()
    points, flags, kept = [], [], []
    for k in range(4):
        start, end = moved[k], moved[(k + 1) % 4]
        if k in chips:  # the corner cut by a short break: a point on each side, the cut a fracture
            prev = moved[k - 1]
            kept.append(len(points))
            points.append(_towards(start, prev, rng.uniform(*CHIP_M)))
            flags.append(True)
            start_pt = _towards(start, end, rng.uniform(*CHIP_M))
        else:
            start_pt = start
        kept.append(len(points))
        points.append(start_pt)
        flags.append(fracture[k])
        if fracture[k]:
            n = max(1, round(math.dist(start_pt, end) / SEG_M))
            for j in range(1, n):
                t = j / n
                jag = rng.uniform(-JAG_M, JAG_M)
                points.append((start_pt[0] + (end[0] - start_pt[0]) * t + jag * normals[k][0],
                               start_pt[1] + (end[1] - start_pt[1]) * t + jag * normals[k][1]))
                flags.append(True)
    return points, flags, kept, (x0, z0, x1, z1)


# --- the pieces -----------------------------------------------------------------------------------------------------

def shard(rng, tile, size, breaks, cell):
    """A shard of `size` broken out of `tile` by `breaks`, its clay in CELLS_PX[cell], lying top up as on the roof
    (the module's piece and frame). LOD0 is the jagged outline, LOD1 its corners only; one box holds it."""
    points, fracture, corners, (x0, z0, x1, z1) = shard_outline(rng, tile, size, breaks)
    uv = _CellUV(tile, cell)
    lod0 = _slab(points, fracture, tile, uv)
    lod1 = _slab([points[i] for i in corners], [fracture[i] for i in corners], tile, uv)
    lo, hi = lod0.bounds()
    shift = kits_vehicle.translation((-(x0 + x1) / 2, -lo[1], -(z0 + z1) / 2))
    for mesh in (lod0, lod1):
        kits_vehicle.transform_range(mesh, (0, len(mesh.vertices)), (0, len(mesh.faces)), shift)
    height = hi[1] - lo[1]
    collision = [{"shape": "box", "center": [0.0, height / 2, 0.0], "size": [x1 - x0, height, z1 - z0],
                  "rotation": [0.0, 0.0, 0.0, 1.0], "material": PHYSICS, "landable": False}]
    snag = [{"kind": "edge", "radius_m": EDGE_SNAG_R_M,
             "points": [list(shift.point((points[j][0], camber_y(tile, points[j][0]) + tile.thickness, points[j][1])))
                        for j in run]} for run in fracture_runs(fracture)]
    return {"lods": [lod0, lod1], "collision": collision, "snag": snag,
            "materials": [{"slot": s, "physics": PHYSICS} for s in lod0.slots()],
            "volume_m3": abs(_signed_area(points)) * tile.thickness}


def fracture_runs(fracture):
    """The outline's runs of consecutive fracture edges, each as the point indices from its first edge's start to its
    last edge's end; a single closed run (its first point repeated) when every edge is a fracture."""
    n = len(fracture)
    if all(fracture):
        return [list(range(n)) + [0]]
    start = next(i for i in range(n) if not fracture[i])
    runs, run = [], []
    for k in range(1, n + 1):  # from the edge after a factory edge round to that factory edge
        i = (start + k) % n
        if fracture[i]:
            run.append(i)
        elif run:
            runs.append(run + [(run[-1] + 1) % n])
            run = []
    return runs


def whole(tile, cell):
    """A whole tile, its clay in CELLS_PX[cell], lying top up and resting on its tail and its nib, the head lifted
    by the nib (the module's piece and frame). LOD0 follows the camber in CAMBER_SEGMENTS[0] strips and carries the
    nib, LOD1 has two strips and no nib; a box per half, each along its half's chord, holds it."""
    uv = _CellUV(tile, cell)
    lods = [_whole_mesh(tile, uv, segments, nib=(k == 0)) for k, segments in enumerate(CAMBER_SEGMENTS)]
    collision = []
    for side in (-1, 1):  # the tail half, then the head half, each a box along its chord
        alpha = -side * math.atan2(tile.camber, tile.length / 2)
        pts = [v for v in lods[0].vertices if side * v[0] >= -1e-12]
        collision.append(_oriented_box(pts, alpha))
    # Resting on the tail's lower edge and the nib's inner foot: the turn about the tail that lifts that foot to y = 0
    rest = kits_vehicle.axis_rotation((-tile.length / 2, 0.0, 0.0), (0.0, 0.0, 1.0), rest_angle(tile))
    for mesh in lods:
        kits_vehicle.transform_range(mesh, (0, len(mesh.vertices)), (0, len(mesh.faces)), rest)
    lo, hi = lods[0].bounds()
    shift = kits_vehicle.translation((-(lo[0] + hi[0]) / 2, -lo[1], 0.0))
    for mesh in lods:
        kits_vehicle.transform_range(mesh, (0, len(mesh.vertices)), (0, len(mesh.faces)), shift)
    rigid = rest.then(shift)
    return {"lods": lods, "collision": [kits_vehicle.transform_shape(c, rigid) for c in collision], "snag": [],
            "materials": [{"slot": TILE_SLOT, "physics": PHYSICS}],
            "volume_m3": tile.length * tile.width * tile.thickness + NIB_M[0] * NIB_M[1] * NIB_M[2]}


def rest_angle(tile):
    """How far a whole tile lying top up turns about its tail to rest on its nib: the nib's inner foot, NIB_M[0] in
    from the head and NIB_M[2] under it, on the ground with the tail's lower edge."""
    return math.atan2(NIB_M[2], tile.length - NIB_M[0])


def _slab(points, fracture, tile, uv):
    """The shard's mesh from its outline: the top and the underside on the camber, each a fan from the outline's
    area centroid, and a rim quad per edge, a fracture edge in FRACTURE_SLOT."""
    mesh = Mesh()
    cx, cz = _area_centroid(points)
    n = len(points)
    lengths = [math.dist(points[i], points[(i + 1) % n]) for i in range(n)]
    perimeter = sum(lengths)
    for top in (True, False):
        lift = tile.thickness if top else 0.0
        centre = mesh.vertex((cx, camber_y(tile, cx) + lift, cz))
        ring = [mesh.vertex((x, camber_y(tile, x) + lift, z)) for x, z in points]
        for i in range(n):
            a, b = ring[i], ring[(i + 1) % n]
            pa, pb = points[i], points[(i + 1) % n]
            mesh.face([centre, a, b], _normal(mesh, [centre, a, b], 1.0 if top else -1.0), TILE_SLOT,
                      [uv.top(cx, cz), uv.top(*pa), uv.top(*pb)])
    along = 0.0
    for i in range(n):
        (ax, az), (bx, bz) = points[i], points[(i + 1) % n]
        ya, yb = camber_y(tile, ax), camber_y(tile, bx)
        quad = [mesh.vertex(p) for p in ((ax, ya, az), (bx, yb, bz), (bx, yb + tile.thickness, bz),
                                         (ax, ya + tile.thickness, az))]
        dx, dz = (bx - ax) / lengths[i], (bz - az) / lengths[i]
        end = along + lengths[i]
        uvs = [uv.rim(along, perimeter, 0.0), uv.rim(end, perimeter, 0.0), uv.rim(end, perimeter, tile.thickness),
               uv.rim(along, perimeter, tile.thickness)]
        mesh.face(quad, (dz, 0.0, -dx), FRACTURE_SLOT if fracture[i] else TILE_SLOT, uvs)
        along = end
    return mesh


def _whole_mesh(tile, uv, segments, nib):
    """A whole tile in the tile's frame: `segments` strips along the camber, top, underside, sides and ends."""
    mesh = Mesh()
    L, W, t = tile.length, tile.width, tile.thickness
    xs = [-L / 2 + L * j / segments for j in range(segments + 1)]
    perimeter = 2 * (L + W)
    for j in range(segments):
        a, b = xs[j], xs[j + 1]
        ya, yb = camber_y(tile, a), camber_y(tile, b)
        for top in (True, False):
            lift = t if top else 0.0
            quad = [(a, ya + lift, -W / 2), (b, yb + lift, -W / 2), (b, yb + lift, W / 2), (a, ya + lift, W / 2)]
            ids = [mesh.vertex(p) for p in quad]
            mesh.face(ids, _normal(mesh, ids, 1.0 if top else -1.0), TILE_SLOT, [uv.top(p[0], p[2]) for p in quad])
        for side in (-1, 1):  # the long sides, distance round the outline from the tail's -z corner
            s0, s1 = (a + L / 2, b + L / 2) if side < 0 else (L + W + L / 2 - a, L + W + L / 2 - b)
            quad = [(a, ya, side * W / 2), (b, yb, side * W / 2), (b, yb + t, side * W / 2), (a, ya + t, side * W / 2)]
            mesh.face([mesh.vertex(p) for p in quad], (0.0, 0.0, float(side)), TILE_SLOT,
                      [uv.rim(s0, perimeter, 0.0), uv.rim(s1, perimeter, 0.0), uv.rim(s1, perimeter, t),
                       uv.rim(s0, perimeter, t)])
    for end in (-1, 1):
        x = end * L / 2
        s0, s1 = (L, L + W) if end > 0 else (2 * L + W, perimeter)
        quad = [(x, 0.0, -end * W / 2), (x, 0.0, end * W / 2), (x, t, end * W / 2), (x, t, -end * W / 2)]
        mesh.face([mesh.vertex(p) for p in quad], (float(end), 0.0, 0.0), TILE_SLOT,
                  [uv.rim(s0, perimeter, 0.0), uv.rim(s1, perimeter, 0.0), uv.rim(s1, perimeter, t),
                   uv.rim(s0, perimeter, t)])
    if nib:  # the lug under the head, its top on the underside
        a, c, d = NIB_M
        x0, x1 = L / 2 - a, L / 2
        y0 = camber_y(tile, x0)
        box = {(sx, sy, sz): (x0 if sx < 0 else x1, -d if sy < 0 else (y0 if sx < 0 else 0.0), sz * c / 2)
               for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)}
        ids = {k: mesh.vertex(p) for k, p in box.items()}
        for axis, sign in ((0, -1), (0, 1), (1, -1), (2, -1), (2, 1)):  # no top: it meets the underside
            keys = [k for k in ids if k[axis] == sign]
            o = [(axis + 1) % 3, (axis + 2) % 3]
            keys.sort(key=lambda k: math.atan2(k[o[1]], k[o[0]]))
            normal = tuple(float(sign) if a_ == axis else 0.0 for a_ in range(3))
            mesh.face([ids[k] for k in keys], normal, TILE_SLOT,
                      [uv.rim(L + W / 2 + box[k][2], perimeter, box[k][1] + d) for k in keys])
    return mesh


def _normal(mesh, ids, up):
    """The unit normal of a planar face on the camber (Newell), on the side of `up` (+1 the top, -1 the underside):
    the audit measures against the plane of the normal a face is given, so it must be the face's own."""
    pts = [mesh.vertices[i] for i in ids]
    n = [0.0, 0.0, 0.0]
    for p, q in zip(pts, pts[1:] + pts[:1]):
        n[0] += (p[1] - q[1]) * (p[2] + q[2])
        n[1] += (p[2] - q[2]) * (p[0] + q[0])
        n[2] += (p[0] - q[0]) * (p[1] + q[1])
    length = math.sqrt(sum(c * c for c in n)) * (1 if n[1] * up > 0 else -1)
    return tuple(c / length for c in n)


def _oriented_box(points, alpha):
    """The box of `points` with its x axis turned by `alpha` about +z (from +x towards +y): the tightest box in that
    orientation."""
    c, s = math.cos(alpha), math.sin(alpha)
    us = [p[0] * c + p[1] * s for p in points]
    ns = [-p[0] * s + p[1] * c for p in points]
    zs = [p[2] for p in points]
    mu, mn, mz = (min(us) + max(us)) / 2, (min(ns) + max(ns)) / 2, (min(zs) + max(zs)) / 2
    return {"shape": "box", "center": [mu * c - mn * s, mu * s + mn * c, mz],
            "size": [max(us) - min(us), max(ns) - min(ns), max(zs) - min(zs)],
            "rotation": [0.0, 0.0, math.sin(alpha / 2), math.cos(alpha / 2)], "material": PHYSICS, "landable": False}


def _signed_area(points):
    return sum(x0 * z1 - x1 * z0 for (x0, z0), (x1, z1) in zip(points, points[1:] + points[:1])) / 2


def _area_centroid(points):
    a = cx = cz = 0.0
    for (x0, z0), (x1, z1) in zip(points, points[1:] + points[:1]):
        cross = x0 * z1 - x1 * z0
        a += cross
        cx += (x0 + x1) * cross
        cz += (z0 + z1) * cross
    return cx / (3 * a), cz / (3 * a)


def _interval(rng, mode, extent, span):
    """Where a shard's side pair sits across a tile's span: the whole span, against one edge, or inside it."""
    half = span / 2
    if mode == "full":
        return -half, half
    if mode == "end":
        extent = min(extent, span - MARGIN_M)
        return (half - extent, half) if rng.random() < 0.5 else (-half, -half + extent)
    extent = min(extent, span - 2 * MARGIN_M)
    start = rng.uniform(-half + MARGIN_M, half - MARGIN_M - extent)
    return start, start + extent


def _towards(a, b, d):
    length = math.dist(a, b)
    return (a[0] + (b[0] - a[0]) * d / length, a[1] + (b[1] - a[1]) * d / length)
