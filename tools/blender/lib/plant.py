"""Physics of a vendor plant's mesh (w4a-farmland-vegetation design 3 to 7), in plain Python, deterministic: the wood
as capsule chains that follow its tubes, the foliage as the samples its cards draw (alpha at or over the cutoff),
the crown or the bush as a compliant shell of spheres over those samples, the wind volumes with the porosity the
samples measure, and the snag records. Everything is in the prefab's frame (asset space, metres, y up, the ground at
y 0).

Wood (design 4): the tubes' level sets. Each connected tube system is measured from its root (a ground system from
its base band, ROOT_BAND_M over its lowest point; any other from its vertex nearest a ground system, found by
nearest points taken alternately from its centroid) by geodesic distance along the mesh's edges; the level sets at
every `step` of that distance cut the triangles in rings. A ring is a node: its centre the length-weighted mean of
its crossing segments, its radius their mean distance from it, its points the crossings. A ring's parent is the
ring one level below that the band between them joins it to. The node tree splits into members at every fork; a
member is cut into the longest segments (greedy, at most `max_len`) whose rings' points lie within `tol` of one
round capsule; a section that is not round even over one step (scanned wood's lobes, a merged fork, a root flare)
takes the capsule of its mean over a few steps (`fit_capsules`). A member thinner than `min_radius` gets no capsule: it is a twig,
under the shell.

Foliage (design 5): every triangle of a card slot sampled on a barycentric lattice at most `spacing` apart, a sample
kept where its texel's alpha passes the material's cutoff: what Unity draws, not the quad round it.

Shell (design 6): cells of CELL_M; the mass is every cell within `depth` (the foliage's half depth: a card stands
for a spray of real thickness) of a sample cell. A candidate is the largest ball round a cell centre that stays
inside the mass (the Euclidean distance transform of the cells outside it, less half a cell diagonal, at most
`max_radius`), so every point of its surface lies within depth + half a cell diagonal of a sample cell. Sample
cells are taken roomiest first: one that no piece yet holds within `reach` takes the roomiest candidate that holds
it (the nearest of equals), and every sample cell that piece holds is done. A piece whose every cell another piece
also holds is then dropped, smallest first. Pieces overlap, as the crown's own sprays do.

Wind (design 7): one box over the foliage samples (the crown or the bush) and, under a crown standing above the
ground, a solid column of the trunk's diameter. Optical porosity beta = 1 - (opaque area averaged over BEARINGS) /
(D x height), D = perimeter / pi the box's mean width as MapWind takes it: the opaque area is the box's frontal
rectangle in WIND_CELL_M cells at each bearing, a cell opaque when a drawn sample falls in it or a wood triangle
covers its centre."""
import heapq
import math

ROOT_BAND_M = 0.05     # a ground tube's root: its vertices this far over its lowest one (its base ring, assumed)
GROUND_REACH_M = 0.3   # a tube system whose lowest point is this close to the ground stands on it (assumed)
WELD_M = 0.001         # vertices this close are one (a split normal or UV seam, not a gap)
CELL_M = 0.1           # the shell's cells
WIND_CELL_M = 0.05     # the porosity raster
BEARINGS = (0.0, 45.0, 90.0, 135.0)  # a projection along a bearing and along its opposite are the same
SURFACE_DIRECTIONS = 162  # standoff and coverage checks: icosphere level 2 directions


# --- small vector helpers -------------------------------------------------------------------------------------------

def _sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def _dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def _dist(a, b):
    return math.sqrt((a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2 + (a[2] - b[2]) ** 2)


def _lerp(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t)


def segment_distance(p, a, b):
    """The distance from p to the segment a-b."""
    ab, ap = _sub(b, a), _sub(p, a)
    den = _dot(ab, ab)
    t = 0.0 if den == 0 else max(0.0, min(1.0, _dot(ap, ab) / den))
    return _dist(p, _lerp(a, b, t))


class _Union:
    def __init__(self):
        self.parent = {}

    def find(self, a):
        parent = self.parent
        parent.setdefault(a, a)
        root = a
        while parent[root] != root:
            root = parent[root]
        while parent[a] != root:
            parent[a], a = root, parent[a]
        return root

    def join(self, a, b):
        ra, rb = self.find(a), self.find(b)
        if ra != rb:
            if rb < ra:
                ra, rb = rb, ra
            self.parent[rb] = ra


def weld(points, tris):
    """(points, tris) with vertices within WELD_M merged, so a seam is no gap in a tube."""
    key, out, remap = {}, [], []
    for p in points:
        k = (round(p[0] / WELD_M), round(p[1] / WELD_M), round(p[2] / WELD_M))
        if k not in key:
            key[k] = len(out)
            out.append(p)
        remap.append(key[k])
    welded = []
    for t in tris:
        a, b, c = remap[t[0]], remap[t[1]], remap[t[2]]
        if a != b and b != c and a != c:
            welded.append((a, b, c))
    return out, welded


def components(tris):
    """The connected triangle sets of `tris` (by shared vertices), as lists of triangle indices, in order of their
    first triangle."""
    union = _Union()
    for a, b, c in tris:
        union.join(a, b)
        union.join(a, c)
    groups = {}
    for i, t in enumerate(tris):
        groups.setdefault(union.find(t[0]), []).append(i)
    return list(groups.values())


# --- wood -----------------------------------------------------------------------------------------------------------

def skeleton(points, tris, step):
    """The tube systems' node tree and each vertex's node: (nodes [{"centre", "radius", "parent", "system",
    "level"}], {vertex: node}), a root node per system (level -1, its root's centre, radius 0); a vertex belongs to
    the ring of the nearer level its band joins it to. `points` and `tris` welded."""
    systems = components(tris)
    lowest = [min(points[v][1] for i in s for v in tris[i]) for s in systems]
    grounded = [k for k, low in enumerate(lowest) if low <= GROUND_REACH_M]
    ground_vertices = sorted({v for k in grounded for i in systems[k] for v in tris[i]})
    grid = _PointGrid([points[v] for v in ground_vertices], 0.25)
    nodes, vertex_node = [], {}
    for k, system in enumerate(systems):
        vertices = sorted({v for i in system for v in tris[i]})
        if k in grounded or not ground_vertices:
            low = min(points[v][1] for v in vertices)
            sources = [v for v in vertices if points[v][1] <= low + ROOT_BAND_M]
        else:  # the vertex nearest a ground system: nearest points of the two, alternately, from the centroid
            centroid = tuple(sum(points[v][a] for v in vertices) / len(vertices) for a in range(3))
            g = grid.nearest(centroid)[1]
            for _ in range(8):
                v = min(vertices, key=lambda v: (_dist(points[v], grid.points[g]), v))
                g2 = grid.nearest(points[v])[1]
                if g2 == g:
                    break
                g = g2
            sources = [v]
        distance = _geodesic(points, [tris[i] for i in system], sources)
        root = len(nodes)
        nodes.append({"centre": tuple(sum(points[v][a] for v in sources) / len(sources) for a in range(3)),
                      "radius": 0.0, "parent": None, "system": k, "level": -1, "ring": [points[v] for v in sources]})
        _level_rings(points, [tris[i] for i in system], distance, step, root, k, nodes, vertex_node)
    return nodes, vertex_node


def _geodesic(points, tris, sources):
    adjacency = {}
    for a, b, c in tris:
        for u, v in ((a, b), (b, c), (c, a)):
            w = _dist(points[u], points[v])
            adjacency.setdefault(u, {})[v] = w
            adjacency.setdefault(v, {})[u] = w
    distance = {v: 0.0 for v in sources}
    heap = [(0.0, v) for v in sorted(sources)]
    while heap:
        d, u = heapq.heappop(heap)
        if d > distance[u]:
            continue
        for v, w in sorted(adjacency[u].items()):
            if d + w < distance.get(v, math.inf):
                distance[v] = d + w
                heapq.heappush(heap, (d + w, v))
    return distance


def _level_rings(points, tris, distance, step, root, system, nodes, vertex_node):
    """Appends the rings of the level sets d = (j + 0.5) step of one system to `nodes`, each with its parent."""
    crossings = {}   # (level, edge) -> point
    segments = {}    # level -> [(crossing, crossing)]
    for tri in tris:
        d = [distance[v] for v in tri]
        lo, hi = min(d), max(d)
        first = int(math.floor(lo / step - 0.5)) + 1
        for j in range(max(first, 0), int(math.floor(hi / step - 0.5)) + 1):
            level = (j + 0.5) * step
            if not lo < level <= hi:
                continue
            ends = []
            for u, v in ((tri[0], tri[1]), (tri[1], tri[2]), (tri[2], tri[0])):
                du, dv = distance[u], distance[v]
                if (du < level <= dv) or (dv < level <= du):
                    edge = (u, v) if u < v else (v, u)
                    if (j, edge) not in crossings:
                        a, b = (u, v) if du < dv else (v, u)
                        t = (level - distance[a]) / (distance[b] - distance[a])
                        crossings[(j, edge)] = _lerp(points[a], points[b], t)
                    ends.append((j, edge))
            if len(ends) == 2:
                segments.setdefault(j, []).append((ends[0], ends[1]))
    union = _Union()
    for j in sorted(segments):
        for a, b in segments[j]:
            union.join(a, b)
    rings = {}
    for key in sorted(crossings):
        rings.setdefault(union.find(key), []).append(key)
    ring_of = {key: r for r, keys in rings.items() for key in keys}
    # each ring's centre and radius from its segments
    length, centre_sum = {}, {}
    for j in sorted(segments):
        for a, b in segments[j]:
            r = ring_of[a]
            pa, pb = crossings[a], crossings[b]
            w = _dist(pa, pb)
            m = _lerp(pa, pb, 0.5)
            length[r] = length.get(r, 0.0) + w
            s = centre_sum.setdefault(r, [0.0, 0.0, 0.0])
            for i in range(3):
                s[i] += m[i] * w
    node_of = {}
    for r in sorted(rings, key=lambda r: (r[0], r[1])):
        if length.get(r, 0.0) <= 0:
            continue
        c = tuple(centre_sum[r][i] / length[r] for i in range(3))
        spread = 0.0
        for a, b in segments[r[0]]:
            if ring_of[a] == r:
                pa, pb = crossings[a], crossings[b]
                spread += _dist(_lerp(pa, pb, 0.5), c) * _dist(pa, pb)
        node_of[r] = len(nodes)
        nodes.append({"centre": c, "radius": spread / length[r], "parent": None, "system": system, "level": r[0],
                      "ring": [crossings[key] for key in rings[r]]})
    # parents: rings of consecutive levels joined through the band between them
    band = _Union()
    vertex_band = {}
    for v, d in distance.items():
        vertex_band[v] = int(math.floor(d / step - 0.5))  # band j holds levels j .. j + 1
    for tri in tris:
        for u, v in ((tri[0], tri[1]), (tri[1], tri[2]), (tri[2], tri[0])):
            if vertex_band[u] == vertex_band[v]:
                band.join(("v", u), ("v", v))
    for (j, edge), _ in sorted(crossings.items()):
        r = ring_of[(j, edge)]
        if r not in node_of:
            continue
        u, v = edge
        lower, upper = (u, v) if distance[u] < distance[v] else (v, u)
        band.join(("r", node_of[r], "up"), ("v", upper) if vertex_band[upper] == j else ("e", j, edge))
        band.join(("r", node_of[r], "down"), ("v", lower) if vertex_band[lower] == j - 1 else ("e", j - 1, edge))
    by_level = {}
    for r, n in node_of.items():
        by_level.setdefault(r[0], []).append(n)
    for j in sorted(by_level):
        for n in by_level[j]:
            if j == 0:
                nodes[n]["parent"] = root
                continue
            joined = [m for m in by_level.get(j - 1, []) if band.find(("r", m, "up")) == band.find(("r", n, "down"))]
            if joined:
                nodes[n]["parent"] = max(joined, key=lambda m: (nodes[m]["radius"], -m))
            else:  # a ring the band does not join (a level skipped by a long edge): the nearest ring below
                below = [m for level in range(j - 1, -1, -1) for m in by_level.get(level, [])]
                nodes[n]["parent"] = min(below, key=lambda m: (_dist(nodes[m]["centre"], nodes[n]["centre"]), m)) \
                    if below else root
    ups, downs = {}, {}
    for n in node_of.values():
        ups.setdefault(band.find(("r", n, "up")), []).append(n)
        downs.setdefault(band.find(("r", n, "down")), []).append(n)
    for v in sorted(distance):
        j, key = vertex_band[v], band.find(("v", v))
        here = [n for n in ups.get(key, []) if nodes[n]["level"] == j]
        above = [n for n in downs.get(key, []) if nodes[n]["level"] == j + 1]
        nearer_here = distance[v] - (j + 0.5) * step < (j + 1.5) * step - distance[v]
        pick = (here or above) if nearer_here else (above or here)
        vertex_node[v] = min(pick, key=lambda n: (_dist(nodes[n]["centre"], points[v]), n)) if pick else root


class _PointGrid:
    def __init__(self, points, cell):
        self.points, self.cell, self.cells = points, cell, {}
        for i, p in enumerate(points):
            self.cells.setdefault(self._key(p), []).append(i)

    def _key(self, p):
        return tuple(int(math.floor(c / self.cell)) for c in p)

    def nearest(self, p):
        if not self.points:
            return math.inf, None
        cx, cy, cz = self._key(p)
        best, which = math.inf, None
        for ring in range(0, 1000):
            for dx in range(-ring, ring + 1):
                for dy in range(-ring, ring + 1):
                    for dz in range(-ring, ring + 1):
                        if max(abs(dx), abs(dy), abs(dz)) != ring:
                            continue
                        for i in self.cells.get((cx + dx, cy + dy, cz + dz), ()):
                            d = _dist(p, self.points[i])
                            if d < best:
                                best, which = d, i
            if best <= ring * self.cell:
                return best, which
        return best, which


def members(nodes):
    """The node tree split at its forks: lists of node indices, each starting at its fork (or its system's root)
    and ending at the next fork or a free tip, with a flag for the free tip."""
    children = {}
    for i, n in enumerate(nodes):
        if n["parent"] is not None:
            children.setdefault(n["parent"], []).append(i)
    out = []
    for start in sorted(i for i, n in enumerate(nodes) if n["parent"] is None):
        stack = [(start, c) for c in sorted(children.get(start, []), reverse=True)]
        while stack:
            fork, node = stack.pop()
            chain = [fork, node]
            while len(children.get(node, [])) == 1:
                node = children[node][0]
                chain.append(node)
            kids = children.get(node, [])
            out.append({"nodes": chain, "free_tip": not kids})
            stack.extend((node, c) for c in sorted(kids, reverse=True))
    return out


def fit_capsules(nodes, member, max_len, min_radius, tol, tip=None):
    """The capsules [(a, b, radius)] along one member, one per segment, or [] for a twig (mean ring radius under
    min_radius). The fork node the member starts at lends its centre, not its ring; a member leaving a system's root
    starts at its first ring. `tip`, a free tip's farthest vertex, extends the member so its last cap ends there:
    the last ring stands up to a step short of it. Each segment's capsule runs from ring centre to ring centre at
    its rings' mean distance from that axis (`round_capsule`). A round tube's segment is the longest (at most
    max_len, no farther than two failures past the last success) whose rings lie within tol of its capsule, and
    within THIN_SHARE of its radius on a thin member; where even one step is not that round (scanned wood's lobes,
    a merged fork, a root flare) the segment spans LUMPY_SPAN steps and its capsule stands for the section's mean."""
    chain = member["nodes"]
    radii = [nodes[i]["radius"] for i in chain[1:]]
    if not radii or sum(radii) / len(radii) < min_radius:
        return []
    if nodes[chain[0]]["parent"] is None:  # from a system's root: its first ring, not the root band's mean
        chain = chain[1:]                   # (spread roots or an attachment vertex), which the cap reaches past
        if len(chain) < 2:
            c = nodes[chain[0]]["centre"]
            return [(c, c, max(radii[0], min_radius))]
    centres = [nodes[i]["centre"] for i in chain]
    rings = [[]] + [nodes[i]["ring"] for i in chain[1:]]
    if tip is not None and len(centres) >= 2:
        d = _sub(tip, centres[-1])
        length = math.sqrt(_dot(d, d))
        if length > radii[-1]:
            centres.append(_lerp(centres[-1], tip, (length - radii[-1]) / length))
            rings.append([])

    def fit(i, j):
        return round_capsule([p for ring in rings[i:j + 1] for p in ring], centres[i], centres[j], min_radius)

    out, i = [], 0
    while i < len(centres) - 1:
        capsule, residual = fit(i, i + 1)
        bound = min(tol, THIN_SHARE * capsule[2])
        if residual > bound:  # not round even over one step: the section's mean over LUMPY_SPAN steps
            j = min(i + LUMPY_SPAN, len(centres) - 1)
            out.append(fit(i, j)[0])
            i = j
            continue
        best, misses = (i + 1, capsule), 0
        for j in range(i + 2, len(centres)):
            if _dist(centres[i], centres[j]) > max_len or misses >= 2:
                break
            capsule, residual = fit(i, j)
            if residual <= min(tol, THIN_SHARE * capsule[2]):
                best, misses = (j, capsule), 0
            else:
                misses += 1
        out.append(best[1])
        i = best[0]
    return out


THIN_SHARE = 0.5   # a thin member's capsule holds its rings within half its radius (assumed), so a bent 2 cm stem is
                   # cut where it bends rather than spanned by a chord its own width off it
LUMPY_SPAN = 3     # a section no round capsule follows spans this many ring steps (assumed: 0.3 m at the 0.1 m step)


def round_capsule(ring_points, a, b, min_radius):
    """((a, b, r), residual): the capsule round the segment a-b at its rings' points' mean distance from the axis,
    and the farthest any of them lies from its surface, either way."""
    t = _sub(b, a)
    length = math.sqrt(_dot(t, t))
    if not ring_points or length == 0:
        return (a, b, min_radius), 0.0
    t = tuple(x / length for x in t)
    dist = []
    for p in ring_points:
        d = _sub(p, a)
        along = _dot(d, t)
        dist.append(math.sqrt(max(_dot(d, d) - along * along, 0.0)))
    r = max(sum(dist) / len(dist), min_radius)
    return (a, b, r), max(abs(x - r) for x in dist)


def _cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


# --- foliage samples --------------------------------------------------------------------------------------------

def opaque_samples(points, tris, uvs, alpha, spacing):
    """The samples of every triangle on a barycentric lattice at most `spacing` apart, kept where `alpha(u, v)` holds:
    a list of points, and per triangle the index range of its samples."""
    out, ranges = [], []
    for (a, b, c), (ua, ub, uc) in zip(tris, uvs):
        pa, pb, pc = points[a], points[b], points[c]
        n = max(1, int(math.ceil(max(_dist(pa, pb), _dist(pb, pc), _dist(pc, pa)) / spacing)))
        first = len(out)
        for i in range(n + 1):
            for j in range(n + 1 - i):
                s, t = i / n, j / n
                w = 1.0 - s - t
                u = w * ua[0] + s * ub[0] + t * uc[0]
                v = w * ua[1] + s * ub[1] + t * uc[1]
                if alpha(u, v):
                    out.append((w * pa[0] + s * pb[0] + t * pc[0], w * pa[1] + s * pb[1] + t * pc[1],
                                w * pa[2] + s * pb[2] + t * pc[2]))
        ranges.append((first, len(out)))
    return out, ranges


# --- the shell --------------------------------------------------------------------------------------------------

def _edt_1d(f, n):
    """Felzenszwalb and Huttenlocher's 1D squared distance transform of f (inf where empty)."""
    d, v, z = [0.0] * n, [0] * n, [0.0] * (n + 1)
    k = 0
    while k < n and f[k] == math.inf:
        k += 1
    if k == n:
        return [math.inf] * n
    v[0], z[0], z[1], k0 = k, -math.inf, math.inf, 0
    for q in range(k + 1, n):
        if f[q] == math.inf:
            continue
        s = ((f[q] + q * q) - (f[v[k0]] + v[k0] * v[k0])) / (2 * q - 2 * v[k0])
        while s <= z[k0]:
            k0 -= 1
            s = ((f[q] + q * q) - (f[v[k0]] + v[k0] * v[k0])) / (2 * q - 2 * v[k0])
        k0 += 1
        v[k0], z[k0], z[k0 + 1] = q, s, math.inf
    k0 = 0
    for q in range(n):
        while z[k0 + 1] < q:
            k0 += 1
        d[q] = (q - v[k0]) ** 2 + f[v[k0]]
    return d


def distance_transform(seeds, shape):
    """The Euclidean distance in cells from every cell of a grid of `shape` (nx, ny, nz) to the nearest seed cell,
    as a flat list indexed x + nx (y + ny z)."""
    nx, ny, nz = shape
    grid = [math.inf] * (nx * ny * nz)
    for x, y, z in seeds:
        grid[x + nx * (y + ny * z)] = 0.0
    for z in range(nz):
        for y in range(ny):
            base = nx * (y + ny * z)
            grid[base:base + nx] = _edt_1d(grid[base:base + nx], nx)
    for z in range(nz):
        for x in range(nx):
            column = [grid[x + nx * (y + ny * z)] for y in range(ny)]
            for y, value in enumerate(_edt_1d(column, ny)):
                grid[x + nx * (y + ny * z)] = value
    for y in range(ny):
        for x in range(nx):
            column = [grid[x + nx * (y + ny * z)] for z in range(nz)]
            for z, value in enumerate(_edt_1d(column, nz)):
                grid[x + nx * (y + ny * z)] = value
    return [math.sqrt(g) for g in grid]


def shell(samples, depth, reach, max_radius):
    """The pieces [(centre, radius)] of the shell over `samples` (design 6) at one foliage half depth: the mass is
    every cell within `depth` of a sample cell; a candidate is the largest ball round a cell centre inside the
    mass (its room: the distance transform of the cells outside the mass, less half a cell diagonal), at most
    `max_radius`. Sample cells are taken roomiest first; one that no piece yet holds within `reach` takes the
    roomiest candidate that holds it, and every sample cell that piece holds within `reach` is done. Returns the
    pieces and {"cells": sample cells, "uncovered": cells no candidate reaches}."""
    if not samples:
        return [], {"cells": 0, "uncovered": 0}
    margin = depth + 2 * CELL_M
    lo = [min(p[a] for p in samples) - margin for a in range(3)]
    hi = [max(p[a] for p in samples) + margin for a in range(3)]
    origin = [math.floor(lo[a] / CELL_M) * CELL_M for a in range(3)]
    shape = tuple(int(math.floor((hi[a] - origin[a]) / CELL_M)) + 1 for a in range(3))
    nx, ny, nz = shape
    targets = sorted({tuple(int(math.floor((p[a] - origin[a]) / CELL_M)) for a in range(3)) for p in samples})
    near = distance_transform(targets, shape)
    limit = depth / CELL_M + 1e-9
    outside = [(x, y, z) for z in range(nz) for y in range(ny) for x in range(nx)
               if near[x + nx * (y + ny * z)] > limit]
    room = distance_transform(outside, shape)
    half = CELL_M * math.sqrt(3) / 2
    radius = [min(v * CELL_M - half, max_radius) for v in room]
    r_max = max(radius)
    if r_max <= 0:
        return [], {"cells": len(targets), "uncovered": len(targets)}
    span = int(math.ceil((r_max + reach) / CELL_M))
    offsets = sorted((math.sqrt(dx * dx + dy * dy + dz * dz) * CELL_M, dx, dy, dz)
                     for dz in range(-span, span + 1) for dy in range(-span, span + 1) for dx in range(-span, span + 1))
    offsets = [o for o in offsets if o[0] <= r_max + reach + 1e-9]
    centre_of = lambda c: tuple(origin[a] + (c[a] + 0.5) * CELL_M for a in range(3))
    target_set = set(targets)
    done, pieces, missed = set(), [], 0

    def roomiest(t):
        """The roomiest candidate holding t within reach, the nearest of equals (offsets run nearest first)."""
        best_r, best = 0.0, None
        tx, ty, tz = t
        for d, dx, dy, dz in offsets:
            x, y, z = tx + dx, ty + dy, tz + dz
            if not (0 <= x < nx and 0 <= y < ny and 0 <= z < nz):
                continue
            r = radius[x + nx * (y + ny * z)]
            if r > best_r and r + reach >= d:
                best_r, best = r, (x, y, z)
                if best_r >= r_max:
                    break
        return best, best_r

    for t in sorted(targets, key=lambda c: (-radius[c[0] + nx * (c[1] + ny * c[2])], c)):
        if t in done:
            continue
        best, r = roomiest(t)
        if best is None:
            missed += 1
            done.add(t)
            continue
        held = [(best[0] + dx, best[1] + dy, best[2] + dz) for d, dx, dy, dz in offsets if d <= r + reach]
        held = [q for q in held if q in target_set]
        pieces.append((centre_of(best), r, held))
        done.update(held)
    # a piece every one of whose cells another piece also holds is dropped, smallest first
    holders = {}
    for i, (_, _, held) in enumerate(pieces):
        for q in held:
            holders[q] = holders.get(q, 0) + 1
    kept = [True] * len(pieces)
    for i in sorted(range(len(pieces)), key=lambda i: (pieces[i][1], i)):
        if all(holders[q] > 1 for q in pieces[i][2]):
            kept[i] = False
            for q in pieces[i][2]:
                holders[q] -= 1
    return [(c, r) for (c, r, _), keep in zip(pieces, kept) if keep], {"cells": len(targets), "uncovered": missed}


def standoff(pieces, samples, directions):
    """The largest distance from a point of a piece's surface (outside every other piece, above the ground) to the
    nearest sample cell's centre, over `directions` per piece, and the piece it is on (the sample cells' distance
    transform: within half a cell diagonal of the distance to the samples themselves)."""
    if not pieces:
        return 0.0, None
    cells = {tuple(int(math.floor(p[a] / CELL_M)) for a in range(3)) for p in samples}
    reach = max(r for _, r in pieces)
    pad = int(math.ceil(2 * reach / CELL_M)) + 2
    lo = [min(c[a] for c in cells) - pad for a in range(3)]
    hi = [max(c[a] for c in cells) + pad for a in range(3)]
    shape = tuple(hi[a] - lo[a] + 1 for a in range(3))
    near = distance_transform([tuple(c[a] - lo[a] for a in range(3)) for c in cells], shape)
    nx, ny = shape[0], shape[1]
    grid = _PointGrid([c for c, _ in pieces], 1.0)
    worst, where = 0.0, None
    for i, (c, r) in enumerate(pieces):
        for d in directions:
            p = (c[0] + d[0] * r, c[1] + d[1] * r, c[2] + d[2] * r)
            if p[1] < 0:
                continue
            if any(j != i and _dist(p, pieces[j][0]) < pieces[j][1] - 1e-9 for j in _near_index(grid, p, reach)):
                continue
            k = [min(max(int(math.floor(p[a] / CELL_M)) - lo[a], 0), shape[a] - 1) for a in range(3)]
            value = near[k[0] + nx * (k[1] + ny * k[2])] * CELL_M
            if value > worst:
                worst, where = value, i
    return worst, where


def _near_index(grid, p, reach):
    span = int(math.ceil(reach / grid.cell))
    cx, cy, cz = grid._key(p)
    for dx in range(-span, span + 1):
        for dy in range(-span, span + 1):
            for dz in range(-span, span + 1):
                yield from grid.cells.get((cx + dx, cy + dy, cz + dz), ())


def most_near(pieces, reach):
    """The largest number of pieces [(centre, radius)] whose surface lies within `reach` of one point, over the
    pieces' centres and the points halfway between two that overlap (where a crowd of them meets), and that point:
    what one drone part of that radius touches at once."""
    if not pieces:
        return 0, None
    big = max(r for _, r in pieces) + reach
    grid = _PointGrid([c for c, _ in pieces], 1.0)
    points = [c for c, _ in pieces]
    for i, (c, r) in enumerate(pieces):
        for j in _near_index(grid, c, 2 * big):
            if j > i and _dist(c, pieces[j][0]) < r + pieces[j][1]:
                points.append(_lerp(c, pieces[j][0], r / (r + pieces[j][1])))
    best, at = 0, None
    for p in points:
        n = sum(1 for j in _near_index(grid, p, big) if _dist(p, pieces[j][0]) <= pieces[j][1] + reach)
        if n > best:
            best, at = n, p
    return best, at


def most_in_cube(boxes, side=2.0, step=0.25):
    """The largest number of axis-aligned boxes [(lo, hi)] meeting one axis-aligned cube of `side`, over cubes whose
    corner lies on a `step` lattice, and that corner (the narrow phase's neighbourhood rule, lib.audit rule (d))."""
    counts = {}
    for lo, hi in boxes:
        a = [int(math.floor((lo[k] - side) / step)) + 1 for k in range(3)]
        b = [int(math.ceil(hi[k] / step)) - 1 for k in range(3)]
        for x in range(a[0], b[0] + 1):
            for y in range(a[1], b[1] + 1):
                for z in range(a[2], b[2] + 1):
                    counts[(x, y, z)] = counts.get((x, y, z), 0) + 1
    if not counts:
        return 0, None
    corner, n = max(sorted(counts.items()), key=lambda kv: kv[1])
    return n, tuple(c * step for c in corner)


# --- wind -------------------------------------------------------------------------------------------------------

def porosity(samples, wood_tris, points, box_lo, box_hi):
    """The optical porosity of the box (design 7) and the per-bearing opaque shares of its frontal rectangle:
    beta = 1 - (opaque area, averaged over BEARINGS) / (D x height), D the box's mean width perimeter / pi, which is
    what MapWind multiplies (1 - beta) by, so the volume blocks exactly the opaque area measured."""
    shares, opaque_total, frontal_total = [], 0.0, 0.0
    for bearing in BEARINGS:
        a = math.radians(bearing)
        across = (math.cos(a), 0.0, -math.sin(a))  # the frontal plane's horizontal axis, the wind along (sin, 0, cos)
        corners = [(x, z) for x in (box_lo[0], box_hi[0]) for z in (box_lo[2], box_hi[2])]
        us = [x * across[0] + z * across[2] for x, z in corners]
        u0, u1 = min(us), max(us)
        nu = max(1, int(math.ceil((u1 - u0) / WIND_CELL_M)))
        nv = max(1, int(math.ceil((box_hi[1] - box_lo[1]) / WIND_CELL_M)))
        cells = set()

        def mark(u, v):
            i, j = int((u - u0) / WIND_CELL_M), int((v - box_lo[1]) / WIND_CELL_M)
            if 0 <= i < nu and 0 <= j < nv:
                cells.add((i, j))

        inside = lambda p: all(box_lo[k] - 1e-9 <= p[k] <= box_hi[k] + 1e-9 for k in range(3))
        for p in samples:
            if inside(p):
                mark(p[0] * across[0] + p[2] * across[2], p[1])
        for tri in wood_tris:
            q = [points[i] for i in tri]
            if not any(inside(p) for p in q):
                continue
            uv = [(p[0] * across[0] + p[2] * across[2], p[1]) for p in q]
            _raster(uv, u0, box_lo[1], nu, nv, cells)
        opaque = len(cells) * WIND_CELL_M ** 2
        frontal = (u1 - u0) * (box_hi[1] - box_lo[1])
        shares.append(min(1.0, opaque / frontal) if frontal > 0 else 0.0)
        opaque_total += min(opaque, frontal)
    height = box_hi[1] - box_lo[1]
    mean_width = 2 * ((box_hi[0] - box_lo[0]) + (box_hi[2] - box_lo[2])) / math.pi  # MapWind's D: perimeter / pi
    frontal_mean = mean_width * height
    if frontal_mean <= 0:
        return 1.0, shares
    beta = max(0.0, 1.0 - opaque_total / len(BEARINGS) / frontal_mean)
    return beta, shares


def _raster(tri, u0, v0, nu, nv, cells):
    """Marks every WIND_CELL_M cell whose centre the 2D triangle covers."""
    (ax, ay), (bx, by), (cx, cy) = tri
    den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
    if abs(den) < 1e-14:
        return
    i0 = max(0, int((min(ax, bx, cx) - u0) / WIND_CELL_M))
    i1 = min(nu - 1, int((max(ax, bx, cx) - u0) / WIND_CELL_M))
    j0 = max(0, int((min(ay, by, cy) - v0) / WIND_CELL_M))
    j1 = min(nv - 1, int((max(ay, by, cy) - v0) / WIND_CELL_M))
    for i in range(i0, i1 + 1):
        x = u0 + (i + 0.5) * WIND_CELL_M
        for j in range(j0, j1 + 1):
            y = v0 + (j + 0.5) * WIND_CELL_M
            s = ((by - cy) * (x - cx) + (cx - bx) * (y - cy)) / den
            t = ((cy - ay) * (x - cx) + (ax - cx) * (y - cy)) / den
            if s >= 0 and t >= 0 and s + t <= 1:
                cells.add((i, j))


# --- sprays -----------------------------------------------------------------------------------------------------

def spine(samples, anchor, step):
    """A spray's centre line from the sample nearest `anchor` (the trunk) to the farthest one: the samples binned by
    their distance from that base every `step`, each bin's mean a point; at least two points."""
    if len(samples) < 2:
        return None
    base = min(samples, key=lambda p: (_dist(p, anchor), p))
    far = max(_dist(p, base) for p in samples)
    if far < step:
        return None
    bins = {}
    for p in samples:
        bins.setdefault(min(int(_dist(p, base) / step), int(far / step)), []).append(p)
    line = [base]
    for k in sorted(bins):
        if k == 0:
            continue
        group = bins[k]
        line.append(tuple(sum(p[a] for p in group) / len(group) for a in range(3)))
    return line if len(line) >= 2 else None


def icosphere_directions(level=2):
    """Unit vectors of an icosphere subdivided `level` times (162 at level 2)."""
    t = (1 + math.sqrt(5)) / 2
    verts = [(-1, t, 0), (1, t, 0), (-1, -t, 0), (1, -t, 0), (0, -1, t), (0, 1, t), (0, -1, -t), (0, 1, -t),
             (t, 0, -1), (t, 0, 1), (-t, 0, -1), (-t, 0, 1)]
    verts = [tuple(c / math.sqrt(_dot(v, v)) for c in v) for v in verts]
    faces = [(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4), (11, 10, 2),
             (10, 7, 6), (7, 1, 8), (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9), (4, 9, 5), (2, 4, 11),
             (6, 2, 10), (8, 6, 7), (9, 8, 1)]
    for _ in range(level):
        cache, new = {}, []

        def mid(i, j):
            key = (min(i, j), max(i, j))
            if key not in cache:
                m = _lerp(verts[i], verts[j], 0.5)
                length = math.sqrt(_dot(m, m))
                verts.append(tuple(c / length for c in m))
                cache[key] = len(verts) - 1
            return cache[key]

        for a, b, c in faces:
            ab, bc, ca = mid(a, b), mid(b, c), mid(c, a)
            new += [(a, ab, ca), (b, bc, ab), (c, ca, bc), (ab, bc, ca)]
        faces = new
    return verts

