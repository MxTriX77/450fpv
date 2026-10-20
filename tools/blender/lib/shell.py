"""Crown contact shells as pieces (p1-v13-shrub, review-physics §3 and PR1): spheres and capsules fitted to the leaf
and twig mass of a crown, so that lib.audit's shell rule holds both ways: (a) every point of the pieces' surface
above the ground stands within ENVELOPE_TOLERANCE_M of a face of the mass; (b) every vertex of the geometry slots (the
twig tubes) lies inside the pieces or within that tolerance outside; (c) every vertex of the card slots within the
asset's card allowance (lib.audit.card_allowance). A plain function of a mesh and its slot names, so V01's per-limb
shells fit the same way. Plain Python in asset space (the ground at y 0); deterministic.

The cap by steps (p1-v01-locust review-physics §2.4 and PR1): the fit runs at each of CAPS pieces in turn and keeps
the first whose pieces hold (b) and (c), else the caps again at RETRY_ROUNDS of steps 3 to 5 (task 1.5: step 5 had
shrunk the last round's pieces off a twig fork of V13_tall's thicket, which no round was left to cover again), else
the last; the fewest pieces that pass are also the cheapest for the narrow phase, and the fitter is not monotone in
its cap. The mass and the candidates are built once, and the pieces'
measured standoffs are shared between the steps.

How, at one cap:
1. The mass: a grid of CELL_M cells, a cell in the mass when its centre lies within MASS_M of a face (or under the
   ground, where nothing is measured); the Euclidean distance transform of the cells outside it gives each cell the
   radius of the largest ball round it that stays in the mass (Felzenszwalb and Huttenlocher's separable
   transform), less half a cell. MASS_M sits under the tolerance by what a ball's surface between cell centres
   gains on them.
2. Candidates, spatial pieces of the mass rather than twig groups (review-physics §3): a sphere at every other cell
   in the mass at its radius, and from every fourth one capsules along DIRECTIONS at LEVELS of its radius, as far
   as the cells along their axis allow that radius. A geometry target none of them reaches (a bare twig, whose thin
   mass the lattice misses; p1-v01-locust) gets a sphere of its own and capsules to its like within ORPHAN_SPAN_M;
   a mass with no such target keeps exactly the lattice's candidates.
3. Greedy cover: the candidate that brings the most geometry vertices within reach (the tolerance less
   TARGET_MARGIN_M; vertices within CLUSTER_M of each other, a tube's ring, count as one) is taken, until every one
   is in reach or the cap is taken; then card vertices beyond their allowance join the targets and the greedy goes
   on.
4. While targets stay out of reach, a swap: each piece in turn is dropped and the best candidate for what is then
   out taken in its place, kept when fewer targets stay out; rounds until one changes nothing.
5. Each piece's standoff measured on its own as the audit measures it (lib.audit.standoff; other pieces only hide
   more of it in the audit); a piece over the tolerance less TARGET_MARGIN_M is shrunk by its excess until it
   holds, the candidate takes the shrunk radius, and steps 3 and 4 cover again what that left out.
"""
import heapq
import math

from . import audit, meta

CAPS = (24, 32, 40, 48, 56, 64)  # p1-v01-locust review-physics §2.4 and PR1: per fit call, the first that passes
MAX_PIECES = CAPS[-1]       # derived there from the narrow phase's cost; audit rule (d) holds it per 2 m cube
CELL_M = 0.05
MASS_M = 0.085              # a ball inside the cells within this of a face stands up to about 2 cm more off them
                            # (measured on V13); step 5 shrinks the few that end over the tolerance
TARGET_MARGIN_M = 0.002     # targets are brought within their tolerance less this
MIN_RADIUS_M = 0.04
LEVELS = (1.0, 0.75, 0.5)
MAX_HALF_M = 1.2            # a capsule reaches at most this far either side of its cell
ORPHAN_SPAN_M = 0.6         # the longest capsule between two targets no lattice candidate reaches
DIRECTIONS = tuple(tuple(c / math.sqrt(sum(x * x for x in d)) for c in d) for d in (
    (1, 0, 0), (0, 1, 0), (0, 0, 1), (1, 1, 0), (1, -1, 0), (1, 0, 1), (1, 0, -1), (0, 1, 1), (0, 1, -1),
    (1, 1, 1), (1, 1, -1), (1, -1, 1), (-1, 1, 1)))
BIN_M = 0.2                 # target bins for the cover sets
CLUSTER_M = 0.01            # vertices closer than this (a twig tube's ring) are one target
ROUNDS = 4                  # of steps 3 to 5, and of swaps within step 4
RETRY_ROUNDS = 16           # of steps 3 to 5 when every cap failed: step 5 can shrink the last round's pieces off a
                            # target that no later round covers again (p1-v01-locust task 1.5)


def fit(mesh, geometry_slots, card_slots, card_allowance, caps=CAPS):
    """The shell over the faces of `geometry_slots` and `card_slots` of `mesh` (a lib.geometry.Mesh, LOD0) as
    (pieces, cap, passes): pieces [(a, b, radius)], a capsule along a-b, a sphere when a == b, from the first of
    `caps` whose pieces hold every geometry vertex within the tolerance and every card vertex within `card_allowance`
    (lib.audit.card_allowance); when none does, the caps once more at RETRY_ROUNDS of steps 3 to 5; else from the last
    cap, whose misses the audit then names. A fit that passes at some cap never takes the retry."""
    index = audit.FaceIndex(mesh, set(geometry_slots) | set(card_slots))
    if not index.faces:
        return [], caps[0], True
    geometry, cards = _vertices(mesh, geometry_slots), _vertices(mesh, card_slots)
    mass = _Mass(index, geometry + cards)
    start = _Cover(mass.candidates())
    start.add(geometry, audit.ENVELOPE_TOLERANCE_M - TARGET_MARGIN_M)
    start.extend(mass.on([start.targets[t][0] for t in start.orphans()]))
    standoffs = {}
    for rounds in (ROUNDS, RETRY_ROUNDS):
        for cap in caps:
            pieces = _fit(start.copy(), index, cards, card_allowance - TARGET_MARGIN_M, cap, standoffs, rounds)
            if (all(_outside(v, pieces) <= audit.ENVELOPE_TOLERANCE_M for v in geometry)
                    and all(_outside(v, pieces) <= card_allowance for v in cards)):
                return pieces, cap, True
    return pieces, cap, False


def _fit(cover, index, cards, card_reach, max_pieces, standoffs, rounds=ROUNDS):
    """Steps 3 to 5 at one cap, from a fresh cover of the geometry targets, `rounds` times at most."""
    pieces, carded = [], set()
    for _ in range(rounds):
        cover.greedy(pieces, max_pieces)
        missed = [v for v in cards if v not in carded and _outside(v, pieces) > card_reach]
        carded.update(missed)
        cover.add(missed, card_reach)
        cover.greedy(pieces, max_pieces)
        cover.swap(pieces)
        changes = _hold(pieces, index, standoffs)
        if not changes:
            break
        cover.replace(changes)
    return pieces


def _vertices(mesh, slots):
    return sorted({mesh.vertices[i] for indices, _, _, slot in mesh.faces if slot in slots for i in indices})


class _Mass:
    """The grid of step 1 and the candidates of step 2."""

    def __init__(self, index, points):
        pad = MASS_M + 2 * CELL_M
        self.lo = [min(p[a] for p in points) - pad for a in range(3)]
        self.n = [int((max(p[a] for p in points) + pad - self.lo[a]) / CELL_M) + 1 for a in range(3)]
        nx, ny, nz = self.n
        inside = [[[self.centre((i, j, k))[1] < 0 for k in range(nz)] for j in range(ny)] for i in range(nx)]
        for face in index.faces:  # a cell is in the mass when some face lies within MASS_M of its centre
            lo = [max(0, math.ceil((face.lo[a] - MASS_M - self.lo[a]) / CELL_M)) for a in range(3)]
            hi = [min(self.n[a] - 1, math.floor((face.hi[a] + MASS_M - self.lo[a]) / CELL_M)) for a in range(3)]
            for i in range(lo[0], hi[0] + 1):
                for j in range(lo[1], hi[1] + 1):
                    row = inside[i][j]
                    for k in range(lo[2], hi[2] + 1):
                        if not row[k] and audit._face_distance(self.centre((i, j, k)), face) <= MASS_M:
                            row[k] = True
        self.inside = inside
        self.squared = _transform(inside, self.n)

    def centre(self, cell):
        return tuple(self.lo[a] + cell[a] * CELL_M for a in range(3))

    def radius(self, p):
        """The radius of the largest ball round the cell nearest to p that stays in the mass, less half a cell."""
        cell = [round((p[a] - self.lo[a]) / CELL_M) for a in range(3)]
        if not all(0 <= cell[a] < self.n[a] for a in range(3)):
            return math.inf if p[1] < 0 else -1.0
        return math.sqrt(self.squared[cell[0]][cell[1]][cell[2]]) * CELL_M - CELL_M / 2

    def candidates(self):
        """[(a, b, radius)] of step 2."""
        out = []
        nx, ny, nz = self.n
        for i in range(0, nx, 2):
            for j in range(0, ny, 2):
                for k in range(0, nz, 2):
                    if not self.inside[i][j][k]:
                        continue
                    c = self.centre((i, j, k))
                    r = self.radius(c)
                    if r < MIN_RADIUS_M or c[1] + r < 0:
                        continue
                    out.append((c, c, r))
                    if i % 4 or j % 4 or k % 4:
                        continue
                    for level in LEVELS:
                        if r * level < MIN_RADIUS_M:
                            continue
                        for d in DIRECTIONS:
                            ends = [self._end(c, d, sign, r * level) for sign in (-1, 1)]
                            if _dist(*ends) >= 2 * CELL_M:
                                out.append((ends[0], ends[1], r * level))
        return out

    def _end(self, c, d, sign, r):
        s = 0.0
        while s < MAX_HALF_M and self.radius(tuple(c[a] + sign * (s + CELL_M) * d[a] for a in range(3))) >= r:
            s += CELL_M
        return tuple(c[a] + sign * s * d[a] for a in range(3))

    def on(self, points):
        """Step 2's candidates on targets that none of its own reaches (a bare twig's thin mass, whose axis runs
        between the lattice's cells; p1-v01-locust): a sphere on each point, and a capsule between every two within
        ORPHAN_SPAN_M, at the least radius of the mass along it, each when that is MIN_RADIUS_M or more."""
        out = []
        for i, p in enumerate(points):
            r = self.radius(p)
            if r >= MIN_RADIUS_M:
                out.append((p, p, r))
            for q in points[i + 1:]:
                length = _dist(p, q)
                if not CELL_M <= length <= ORPHAN_SPAN_M:
                    continue
                n = math.ceil(length / (CELL_M / 2))
                r = min(self.radius(tuple(p[a] + (q[a] - p[a]) * k / n for a in range(3))) for k in range(n + 1))
                if r >= MIN_RADIUS_M:
                    out.append((p, q, r))
        return out


class _Cover:
    """The targets (point, reach) of steps 3 and 4 and each candidate's cover of them."""

    def __init__(self, candidates):
        self.candidates, self.targets, self.bins, self.reach = candidates, [], {}, 0.0
        self.covers = []

    def orphans(self):
        """The targets no candidate brings within reach."""
        covered = set()
        for cover in self.covers:
            covered.update(cover)
        return [t for t in range(len(self.targets)) if t not in covered]

    def extend(self, candidates):
        """More candidates, with their covers of every target."""
        everything = range(len(self.targets))
        for candidate in candidates:
            self.candidates.append(candidate)
            self.covers.append(self._cover(*candidate, everything))

    def copy(self):
        """An independent cover in the same state, for one cap of the steps."""
        other = _Cover(list(self.candidates))
        other.targets, other.reach = list(self.targets), self.reach
        other.bins = {key: list(members) for key, members in self.bins.items()}
        other.covers = [list(cover) for cover in self.covers]
        return other

    def add(self, points, reach):
        """New targets: the points gathered in CLUSTER_M cells, each cell's mean point brought within `reach` less
        the cell's farthest point from it; the covers are extended to them."""
        if not points:
            return
        first = len(self.targets)
        cells = {}
        for p in points:
            cells.setdefault(tuple(math.floor(c / CLUSTER_M) for c in p), []).append(p)
        for key in sorted(cells):
            group = cells[key]
            point = tuple(sum(p[a] for p in group) / len(group) for a in range(3))
            self.bins.setdefault(tuple(math.floor(c / BIN_M) for c in point), []).append(len(self.targets))
            self.targets.append((point, reach - max(_dist(point, p) for p in group)))
        self.reach = max(self.reach, reach)
        new = range(first, len(self.targets))
        for n, (a, b, r) in enumerate(self.candidates):
            cover = self._cover(a, b, r, new)
            if n < len(self.covers):
                self.covers[n] += cover
            else:
                self.covers.append(cover)

    def replace(self, changes):
        """Candidates step 5 shrank take their shrunk radius, or none when it dropped them."""
        everything = range(len(self.targets))
        for n, candidate in enumerate(self.candidates):
            if candidate in changes:
                new = changes[candidate]
                self.candidates[n] = new if new else (candidate[0], candidate[1], 0.0)
                self.covers[n] = self._cover(*new, everything) if new else []

    def _cover(self, a, b, r, among):
        lo = [math.floor((min(a[x], b[x]) - r - self.reach) / BIN_M) for x in range(3)]
        hi = [math.floor((max(a[x], b[x]) + r + self.reach) / BIN_M) for x in range(3)]
        out = []
        for x in range(lo[0], hi[0] + 1):
            for y in range(lo[1], hi[1] + 1):
                for z in range(lo[2], hi[2] + 1):
                    for t in self.bins.get((x, y, z), ()):
                        if t in among:
                            point, reach = self.targets[t]
                            if _segment_distance2(point, a, b) <= (r + reach) ** 2:
                                out.append(t)
        return out

    def uncovered(self, pieces):
        return {t for t, (point, reach) in enumerate(self.targets) if _outside(point, pieces) > reach}

    def greedy(self, pieces, max_pieces, among=None):
        """Step 3, lazily: taking pieces only lowers the gains on the heap, so a candidate that still beats the next
        one is the best. `among` limits the candidates."""
        left = self.uncovered(pieces)
        heap = [(-len(left.intersection(self.covers[n])), n)
                for n in (range(len(self.candidates)) if among is None else sorted(among))]
        heap = [entry for entry in heap if entry[0]]
        heapq.heapify(heap)
        while heap and left and len(pieces) < max_pieces:
            _, n = heapq.heappop(heap)
            gain = len(left.intersection(self.covers[n]))
            if heap and gain < -heap[0][0]:
                if gain:
                    heapq.heappush(heap, (-gain, n))
                continue
            if gain:
                pieces.append(self.candidates[n])
                left.difference_update(self.covers[n])

    def swap(self, pieces):
        """Step 4: each piece in turn replaced by the best candidate for what is out without it, among the
        candidates that bring any of that within reach, when that leaves fewer targets out."""
        for _ in range(ROUNDS):
            changed = False
            for i in range(len(pieces)):
                out = len(self.uncovered(pieces))
                if not out:
                    return
                rest = pieces[:i] + pieces[i + 1:]
                left = self.uncovered(rest)
                trial = list(rest)
                self.greedy(trial, len(rest) + 1, {n for n, cover in enumerate(self.covers) if not left.isdisjoint(cover)})
                if len(self.uncovered(trial)) < out:
                    pieces[:] = trial[:i] + trial[-1:] + trial[i:-1]
                    changed = True
            if not changed:
                return


def _hold(pieces, index, standoffs):
    """Step 5: every piece whose standoff on its own (lib.audit.standoff: what the audit measures, before other
    pieces hide any of it) is over the tolerance less TARGET_MARGIN_M is shrunk by its excess and a margin and
    measured again, until it holds; one shrunk under MIN_RADIUS_M is dropped. `standoffs` keeps every piece's
    measured standoff. Returns {piece: what it became, None if dropped} for the pieces that changed."""
    limit = audit.ENVELOPE_TOLERANCE_M - TARGET_MARGIN_M
    changes, kept = {}, []
    for piece in pieces:
        a, b, r = piece
        while r >= MIN_RADIUS_M:
            if (a, b, r) not in standoffs:
                standoffs[(a, b, r)] = audit.standoff(meta.capsule(a, b, r, ""), index)[0]
            d = standoffs[(a, b, r)]
            if d <= limit:
                break
            r -= d - limit + TARGET_MARGIN_M
        if r != piece[2]:
            changes[piece] = (a, b, r) if r >= MIN_RADIUS_M else None
        if r >= MIN_RADIUS_M:
            kept.append((a, b, r))
    pieces[:] = kept
    return changes


def _outside(p, pieces):
    """The distance from p to the union of the pieces, 0 inside it (inf with none)."""
    best = math.inf
    for a, b, r in pieces:
        best = min(best, math.sqrt(_segment_distance2(p, a, b)) - r)
        if best <= 0:
            return 0.0
    return best


def _transform(inside, n):
    """Squared distance, in cells, from every cell to the nearest cell outside the mass: three passes of the 1D
    transform (Felzenszwalb and Huttenlocher 2012)."""
    nx, ny, nz = n
    far = float(3 * max(n) ** 2 + 1)
    g = [[[far if inside[i][j][k] else 0.0 for k in range(nz)] for j in range(ny)] for i in range(nx)]
    for i in range(nx):
        for j in range(ny):
            g[i][j] = _transform1(g[i][j])
    for i in range(nx):
        for k in range(nz):
            column = _transform1([g[i][j][k] for j in range(ny)])
            for j in range(ny):
                g[i][j][k] = column[j]
    for j in range(ny):
        for k in range(nz):
            column = _transform1([g[i][j][k] for i in range(nx)])
            for i in range(nx):
                g[i][j][k] = column[i]
    return g


def _transform1(f):
    """The 1D squared distance transform of sampled function f: min over q of (p - q)^2 + f[q], by the lower
    envelope of parabolas."""
    n = len(f)
    v, z = [0] * n, [-math.inf, math.inf] + [0.0] * (n - 1)
    k = 0
    for q in range(1, n):
        s = ((f[q] + q * q) - (f[v[k]] + v[k] * v[k])) / (2 * q - 2 * v[k])
        while s <= z[k]:
            k -= 1
            s = ((f[q] + q * q) - (f[v[k]] + v[k] * v[k])) / (2 * q - 2 * v[k])
        k += 1
        v[k], z[k], z[k + 1] = q, s, math.inf
    out, k = [0.0] * n, 0
    for q in range(n):
        while z[k + 1] < q:
            k += 1
        out[q] = (q - v[k]) ** 2 + f[v[k]]
    return out


def _segment_distance2(p, a, b):
    ab = (b[0] - a[0], b[1] - a[1], b[2] - a[2])
    ap = (p[0] - a[0], p[1] - a[1], p[2] - a[2])
    length2 = ab[0] * ab[0] + ab[1] * ab[1] + ab[2] * ab[2]
    t = 0.0 if length2 == 0 else min(1.0, max(0.0, (ap[0] * ab[0] + ap[1] * ab[1] + ap[2] * ab[2]) / length2))
    dx, dy, dz = ap[0] - t * ab[0], ap[1] - t * ab[1], ap[2] - t * ab[2]
    return dx * dx + dy * dy + dz * dz


def _dist(a, b):
    return math.sqrt((a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2 + (a[2] - b[2]) ** 2)
