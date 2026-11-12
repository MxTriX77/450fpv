"""The air round a mesh we did not generate (w4b-farmland-ruins task 1.2, REAL-05), in plain Python so the tests run
without Blender: lib.slabs fills the space between two faces only where that space is a closed cavity of its own, so a
wall the mesh shows as two skins with nothing between them is two skins.

A voxel grid of `cell` metres over the mesh's bounds, grown by two cells on every side but the ground's: every voxel a
triangle touches is wall (conservatively: the triangle clipped to the voxel's row of cells, its x extent marked, so a
triangle thinner than a cell still walls it off and faces meeting at an edge leave no crack between them); the open
air is every other voxel the grid's sides reach through other air, six-connected. The ground (y below `ground`) is
wall: a grid reaching down to it starts there, and its bottom is then no side. The air the sides do not reach falls
into closed voids, each labelled with its bounding box. A crack narrower than a cell is sealed; a cavity wider than
about three cells keeps free voxels between its two faces' walls. Rows of voxels along x are Python ints, one bit per
voxel, so a flood moves along a row a whole run of air at a time."""
import math
from collections import deque

from . import slabs


class Air:
    def __init__(self, triangles, cell, ground=0.0, pad=2):
        """`triangles` [(tri, ...)] in asset space, metres; only the corners are read."""
        h = cell
        pts = [p for t in triangles for p in t[0]]
        lo = [min(p[k] for p in pts) for k in range(3)]
        hi = [max(p[k] for p in pts) for k in range(3)]
        self.cell, self.ground = h, ground
        self.origin = (math.floor(lo[0] / h) - pad, max(math.floor(ground / h), math.floor(lo[1] / h) - pad),
                       math.floor(lo[2] / h) - pad)
        self.grounded = self.origin[1] == math.floor(ground / h)
        top = [math.floor(hi[k] / h) + pad for k in range(3)]
        self.size = tuple(top[k] - self.origin[k] + 1 for k in range(3))
        nx, ny, nz = self.size
        self.wall = [0] * (ny * nz)  # row iy * nz + iz: bit ix
        for t in triangles:
            self._mark(t[0])
        self.reached = [0] * (ny * nz)
        self.voids = {}  # row -> [(mask, void index)]: the air the outside does not reach
        self.void_boxes = []  # per void, its box (x0, y0, z0, x1, y1, z1) in metres
        self._extents = {}
        self._flood()
        self._label_voids()

    def _mark(self, tri):
        h, o, (nx, ny, nz) = self.cell, self.origin, self.size
        ys = [p[1] for p in tri]
        iy0, iy1 = max(0, math.floor(min(ys) / h) - o[1]), min(ny - 1, math.floor(max(ys) / h) - o[1])
        for iy in range(iy0, iy1 + 1):
            band = _clip(_clip(list(tri), 1, (iy + o[1]) * h, True), 1, (iy + o[1] + 1) * h, False)
            if not band:
                continue
            zs = [p[2] for p in band]
            iz0, iz1 = max(0, math.floor(min(zs) / h) - o[2]), min(nz - 1, math.floor(max(zs) / h) - o[2])
            for iz in range(iz0, iz1 + 1):
                cell = _clip(_clip(band, 2, (iz + o[2]) * h, True), 2, (iz + o[2] + 1) * h, False)
                if not cell:
                    continue
                xs = [p[0] for p in cell]
                ix0 = max(0, math.floor(min(xs) / h) - o[0])
                ix1 = min(nx - 1, math.floor(max(xs) / h) - o[0])
                if ix1 >= ix0:
                    self.wall[iy * nz + iz] |= ((1 << (ix1 - ix0 + 1)) - 1) << ix0

    def _flood(self):
        """Breadth first over the runs of air from the grid's sides: a run is reached whole once any voxel of it is."""
        nx, ny, nz = self.size
        full = (1 << nx) - 1
        wall, reached = self.wall, self.reached
        runs = {}
        queue = deque()

        def reach(r, seeds):
            if r not in runs:
                runs[r] = _runs(full & ~wall[r])
            add = 0
            for m in runs[r]:
                if seeds & m and not reached[r] & m:
                    add |= m
            if add:
                reached[r] |= add
                queue.append((r, add))

        sides = 1 | (1 << (nx - 1))
        for iy in range(ny):
            for iz in range(nz):
                side = iy == ny - 1 or iz in (0, nz - 1) or iy == 0 and not self.grounded
                reach(iy * nz + iz, full if side else sides)
        while queue:
            r, bits = queue.popleft()
            iy, iz = divmod(r, nz)
            for jy, jz in ((iy - 1, iz), (iy + 1, iz), (iy, iz - 1), (iy, iz + 1)):
                if 0 <= jy < ny and 0 <= jz < nz:
                    rr = jy * nz + jz
                    seeds = bits & ~reached[rr] & ~wall[rr]
                    if seeds:
                        reach(rr, seeds)

    def _label_voids(self):
        """Each closed void: its runs, by the same flood over the air the sides did not reach, and its box."""
        h, o, (nx, ny, nz) = self.cell, self.origin, self.size
        full = (1 << nx) - 1
        left = {}
        for r in range(ny * nz):
            bits = full & ~self.wall[r] & ~self.reached[r]
            if bits:
                left[r] = _runs(bits)
        labelled = set()
        for r0 in sorted(left):
            for m0 in left[r0]:
                if (r0, m0) in labelled:
                    continue
                index = len(self.void_boxes)
                box = [nx, -1, ny, -1, nz, -1]
                labelled.add((r0, m0))
                queue = deque([(r0, m0)])
                while queue:
                    r, m = queue.popleft()
                    self.voids.setdefault(r, []).append((m, index))
                    iy, iz = divmod(r, nz)
                    box = [min(box[0], (m & -m).bit_length() - 1), max(box[1], m.bit_length() - 1),
                           min(box[2], iy), max(box[3], iy), min(box[4], iz), max(box[5], iz)]
                    for jy, jz in ((iy - 1, iz), (iy + 1, iz), (iy, iz - 1), (iy, iz + 1)):
                        rr = jy * nz + jz
                        if 0 <= jy < ny and 0 <= jz < nz and rr in left:
                            for mm in left[rr]:
                                if mm & m and (rr, mm) not in labelled:
                                    labelled.add((rr, mm))
                                    queue.append((rr, mm))
                self.void_boxes.append(((box[0] + o[0]) * h, (box[2] + o[1]) * h, (box[4] + o[2]) * h,
                                        (box[1] + 1 + o[0]) * h, (box[3] + 1 + o[1]) * h, (box[5] + 1 + o[2]) * h))

    def reaches(self, p):
        """Whether the outside reaches the voxel of point p (outside the grid: always)."""
        h, o, (nx, ny, nz) = self.cell, self.origin, self.size
        ix, iy, iz = (math.floor(p[k] / h) - o[k] for k in range(3))
        if not (0 <= ix < nx and 0 <= iy < ny and 0 <= iz < nz):
            return True
        return bool(self.reached[iy * nz + iz] >> ix & 1)

    def open_bits(self, fam, j, bits, lo, hi, r):
        """The pixels of `bits`, row j of a lib.slabs family's raster of `r` metres, whose space between the planes
        at offsets lo and hi along the family's normal is no closed cavity of its own: their middle's voxel is open
        air, or lies in a closed void reaching more than a cell past either plane (a hollow its faces close all round,
        the stove's body: two sheets, never a slab ending inside it), or lies off the grid (below the ground, which
        covers it). A middle in a wall voxel is a space too thin for air: closed."""
        h, o, (nx, ny, nz) = self.cell, self.origin, self.size
        reached, voids = self.reached, self.voids
        u = fam.u
        s = (lo + hi) / 2
        b = [(j + 0.5) * r * fam.v[k] + s * fam.n[k] for k in range(3)]
        out = 0
        for i0, i1 in slabs.runs(bits):
            for i in range(i0, i1 + 1):
                a = (i + 0.5) * r
                ix = math.floor((b[0] + a * u[0]) / h) - o[0]
                iy = math.floor((b[1] + a * u[1]) / h) - o[1]
                iz = math.floor((b[2] + a * u[2]) / h) - o[2]
                if not (0 <= ix < nx and 0 <= iy < ny and 0 <= iz < nz):
                    hit = True
                else:
                    row = iy * nz + iz
                    hit = reached[row] >> ix & 1
                    if not hit and row in voids:
                        bit = 1 << ix
                        index = next((k for m, k in voids[row] if m & bit), None)
                        if index is not None:
                            e_lo, e_hi = self._extent(index, fam)
                            hit = e_lo < lo - h or e_hi > hi + h
                if hit:
                    out |= slabs.span(i, i)
        return out

    def _extent(self, index, fam):
        """A void's extent along a family's normal: its box's corners projected."""
        key = (index, fam.key)
        if key not in self._extents:
            x0, y0, z0, x1, y1, z1 = self.void_boxes[index]
            spans = ((x0, x1), (y0, y1), (z0, z1))
            n = fam.n
            self._extents[key] = (sum(min(n[k] * a, n[k] * b) for k, (a, b) in enumerate(spans)),
                                  sum(max(n[k] * a, n[k] * b) for k, (a, b) in enumerate(spans)))
        return self._extents[key]


def _runs(bits):
    """The runs of set bits of an int, each as its mask."""
    out, i = [], 0
    while bits:
        if bits & 1:
            length = (~bits & (bits + 1)).bit_length() - 1
            out.append(((1 << length) - 1) << i)
            bits >>= length
            i += length
        else:
            skip = (bits & -bits).bit_length() - 1
            bits >>= skip
            i += skip
    return out


def _clip(poly, k, value, keep_above):
    """The convex polygon `poly` cut to coordinate k >= value (keep_above) or <= value."""
    out = []
    m = len(poly)
    for a in range(m):
        p, q = poly[a], poly[(a + 1) % m]
        dp = p[k] - value if keep_above else value - p[k]
        dq = q[k] - value if keep_above else value - q[k]
        if dp >= 0:
            out.append(p)
        if (dp >= 0) != (dq >= 0):
            t = dp / (dp - dq)
            out.append(tuple(p[c] + t * (q[c] - p[c]) for c in range(3)))
    return out
