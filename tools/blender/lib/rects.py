"""Axis-aligned rectangles in a plane (p1-h01-adobe-house, task 1.4): the clear space of a jagged opening, a rectangle
less the blocks, stubs and splinters standing in it, as grid cells and as its outline, for the house's breaches, its
crumbled windows, its roof bays and its ceiling holes. Plain Python. A rect is (x0, x1, y0, y1) with x0 < x1, y0 < y1.

The cells of a set of rects are those of the grid cut at every rect's edges, so neighbouring cells share their edges
exactly; the outline of a union of cells walks the edges no two cells share, counter-clockwise, collinear runs merged.
"""

EPS = 1e-9


def grid(bound, rects):
    """The cells (x0, x1, y0, y1) of `bound` cut at every edge of `rects` that crosses it, row by row from the
    bottom left."""
    xs = sorted({bound[0], bound[1]} | {c for r in rects for c in r[:2] if bound[0] + EPS < c < bound[1] - EPS})
    ys = sorted({bound[2], bound[3]} | {c for r in rects for c in r[2:] if bound[2] + EPS < c < bound[3] - EPS})
    return [(x0, x1, y0, y1) for y0, y1 in zip(ys, ys[1:]) for x0, x1 in zip(xs, xs[1:])]


def inside(cell, rect):
    """Whether the cell lies within the rect (its centre does: cells never straddle a rect's edge)."""
    cx, cy = (cell[0] + cell[1]) / 2, (cell[2] + cell[3]) / 2
    return rect[0] - EPS <= cx <= rect[1] + EPS and rect[2] - EPS <= cy <= rect[3] + EPS


def free(bound, blocks):
    """The cells of `bound` that no block covers: the clear space of a rect less the blocks standing in it."""
    return [c for c in grid(bound, blocks) if not any(inside(c, b) for b in blocks)]


def union(rects):
    """The cells of the union of `rects`, on the grid of all their edges."""
    lo = (min(r[0] for r in rects), max(r[1] for r in rects), min(r[2] for r in rects), max(r[3] for r in rects))
    return [c for c in grid(lo, rects) if any(inside(c, r) for r in rects)]


def merge(cells):
    """Grid cells joined into fewer rects covering the same area: runs along each row, then runs of equal width
    stacked up."""
    rows = {}
    for c in sorted(cells, key=lambda c: (c[2], c[0])):
        row = rows.setdefault((c[2], c[3]), [])
        if row and abs(row[-1][1] - c[0]) < EPS:
            row[-1] = (row[-1][0], c[1], c[2], c[3])
        else:
            row.append(c)
    out = []
    for r in sorted((r for row in rows.values() for r in row), key=lambda r: (r[0], r[1], r[2])):
        if out and abs(out[-1][0] - r[0]) < EPS and abs(out[-1][1] - r[1]) < EPS and abs(out[-1][3] - r[2]) < EPS:
            out[-1] = (r[0], r[1], out[-1][2], r[3])
        else:
            out.append(r)
    return out


def area(rects):
    return sum((r[1] - r[0]) * (r[3] - r[2]) for r in rects)


def minus(rect, hole):
    """The parts of `rect` outside `hole`, as at most four rects (the strips left, right, below and above)."""
    x0, x1, y0, y1 = rect
    h0, h1, k0, k1 = max(hole[0], x0), min(hole[1], x1), max(hole[2], y0), min(hole[3], y1)
    if h1 <= h0 + EPS or k1 <= k0 + EPS:
        return [rect]
    out = [(x0, h0, y0, y1), (h1, x1, y0, y1), (h0, h1, y0, k0), (h0, h1, k1, y1)]
    return [r for r in out if r[1] - r[0] > EPS and r[3] - r[2] > EPS]


def outline(cells):
    """The boundary of a union of grid cells as one counter-clockwise loop of (x, y) corners, collinear runs merged.
    Raises ValueError when the union is not one simply connected region (two loops, or two loops meeting at a point)."""
    edges = {}
    for x0, x1, y0, y1 in cells:
        for a, b in (((x0, y0), (x1, y0)), ((x1, y0), (x1, y1)), ((x1, y1), (x0, y1)), ((x0, y1), (x0, y0))):
            if (b, a) in edges:
                del edges[b, a]
            else:
                edges[a, b] = True
    nxt = {}
    for a, b in edges:
        if a in nxt:
            raise ValueError(f"the cells meet at a point at {a}: no single outline")
        nxt[a] = b
    start = min(nxt)
    loop, p = [start], nxt[start]
    while p != start:
        loop.append(p)
        p = nxt[p]
    if len(loop) != len(nxt):
        raise ValueError(f"the cells make {len(nxt) - len(loop)} more boundary edges than one loop: holes or islands")
    merged = [q for i, q in enumerate(loop) if not _collinear(loop[i - 1], q, loop[(i + 1) % len(loop)])]
    return merged


def _collinear(a, b, c):
    return abs((b[0] - a[0]) * (c[1] - b[1]) - (b[1] - a[1]) * (c[0] - b[0])) < EPS
