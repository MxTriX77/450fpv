"""A damaged house's rubble (p1-h01-adobe-house, design decision 7; object sheets houses-and-yard.md §Shared "How the
generator lays the rubble" and lines-roads-debris.md "Shared: the debris kit"). Plain Python, no Blender.

What the damage items took off the house (its `drops`: each a class, a solid volume and where it fell) comes down
bulked: the rubble's volume is BULKING times the removed solid volume (U02's 1.86, the sheet's rule). Each piece of
50 mm and more is a kit piece with its own collision and snags (lib.debris_tile's shards and whole tiles,
lib.debris_timber's battens, boards and rafters, adobe blocks on lib.debris_brick's chamfered box, clay slabs on
lib.debris_tile's shard outline); the heaps hold the rest of each class's bulk, so the heaps' volume plus BULKING
times the pieces' solid volume is BULKING times the removed volume. By where it fell:

- at a wall's foot (tiles and battens off a slope's eaves, the adobe of a crumbled window or a breach, outside or
  in): a heap against the wall, one sloped box per FOOT_STEP_M of foot ("flat-faced blocks"), its top from the
  metre's depth at the wall to the ground HEAP_WIDTH_M out, the foot's width set so its deepest metre is
  `shard_heap_depth` (within the sheet's 1-2 m), each metre as deep as its share of the bulk; and the pieces
  scattered from the wall by exp(-d / λ) of their class (LAMBDA_M), stratified so each metre band holds its share,
  LEE_WIDEN times wider on a wall facing away from the burst (`burst_azimuth_deg`), along the foot as the volume
  fell, lying on the heap or the ground;
- through a ceiling's holes (its boards and clay): a flat layer over the holes and the pieces on it;
- in the attic (a rafter off the plate, its collar): lying on the ceiling where it holds them, else on the floor.

Every heap and layer is `masonry` in the colour of its main class (the "heap" slot for tile, "adobe", "clay"); the
pieces keep their kits' slots (clay tiles, fresh fracture, timber, nails as "iron"). No piece is near-white.
"""
import math

from generators import debris_brick
from lib import debris_tile, debris_timber, kits_vehicle, prism
from lib.geometry import Mesh, uv_upright

BULKING = 1.86                 # urban-edge U02's bulking, the sheet's rubble rule
LAMBDA_M = {"tile": 1.0, "adobe": 1.5, "timber": 2.0}  # the debris kit's spread per class (the sheet)
LEE_WIDEN = 1.75               # the sheet: 1.5-2 times wider away from the burst (the middle)
FOOT_STEP_M = 1.0              # design decision 7: one sloped box per metre of foot
HEAP_WIDTH_M = (1.0, 2.0)      # the sheet: a heap 1-2 m wide along the foot
MIN_HEAP_M = 0.02              # a metre of heap lower than this is left to the pieces (assumed)
PIECES_PER_TILE = 0.3          # loose pieces per tile that came down, the rest crumbs in the heap (assumed)
WHOLE_SHARE = 0.1              # of them whole tiles (assumed: E shows a few whole among the shards)
SHARD_SIZE_M = (0.06, 0.35)    # the sheet's K02 shards, 50 mm and up as geometry
ADOBE_SIZE_M = (0.10, 0.40)    # chunks up to a whole block's 400 mm (the sheet's 250 x 400 x 200 mm block)
ADOBE_PIECE_SHARE = 0.5        # of the adobe's solid volume in blocks and chunks, the rest crumbs (assumed)
ADOBE_CHAMFER_M = (0.008, 0.02)  # a crumbled block's worn arrises, inside the 2 cm zone (assumed)
CLAY_PIECE_SHARE = 0.2         # of a fallen ceiling's clay in slabs (assumed)
SLAB_SIZE_M = (0.3, 1.0)       # design decision 6: plaster slabs 0.3-1 m
SLAB_THICK = (0.3, 1.0)        # a slab's thickness as a share of the clay layer's (assumed)
MIN_TIMBER_M = 0.3             # a fallen member shorter than this is crumbs (K03's shortest piece)
TIMBER_SPLINTERS = 3           # per broken end of a fallen batten or board (K03: 2-10)
FALLEN_SPLINTER_M = 0.08       # their reach (assumed, as the roof's stubs)
HEAP_SLOT = {"tile": "heap", "adobe": "adobe", "timber": "heap", "clay": "clay"}
PIECE_SLOTS = {"tile": "tile", "tile_fracture": "tile_fracture", "timber": "timber",
               "timber_fracture": "timber_fracture", "nail": "iron", "brick": "adobe"}


def travel(params):
    """The blast's direction across the house in plan (x, z): from the burst at `burst_azimuth_deg` (degrees from
    +X towards +Z, where the burst lies seen from the house) through it."""
    a = math.radians(params["burst_azimuth_deg"])
    return (-math.cos(a), -math.sin(a))


def spread(rng, n, lam):
    """n distances from a wall by exp(-d / lam), stratified: piece i at the quantile (i + u) / n, shuffled."""
    d = [-lam * math.log(1 - (i + rng.random()) / n) for i in range(n)]
    rng.shuffle(d)
    return d


def sizes(rng, n, lo, hi):
    """n piece sizes by the kit's power law (the number larger than s goes as s^-2), clipped to [lo, hi],
    stratified."""
    a, b = lo ** -2, hi ** -2
    out = [(a - (i + rng.random()) / n * (a - b)) ** -0.5 for i in range(n)]
    rng.shuffle(out)
    return out


class Foot:
    """A wall's foot, outside or in: `origin` (x, z) its start on the wall's face, `along` and `out` (x, z) unit
    vectors along it and away from the wall, its `length`, the ground `y0` there, the spans along it no heap may
    stand on (a door), and LEE_WIDEN's share for it (`lee`, 0-1)."""

    def __init__(self, name, origin, along, out, length, y0, keep_clear=(), lee=0.0):
        self.name, self.origin, self.along, self.out = name, origin, along, out
        self.length, self.y0, self.keep_clear, self.lee = length, y0, list(keep_clear), lee
        self.metres = [(m, min(m + FOOT_STEP_M, length)) for m in _steps(length)]
        self.bulk = [dict() for _ in self.metres]  # per metre, per class, the core's bulked volume
        self.heap = []                             # per metre: (a0, a1, depth, width, slot)

    def point(self, a, d, y=0.0):
        return (self.origin[0] + a * self.along[0] + d * self.out[0], y,
                self.origin[1] + a * self.along[1] + d * self.out[1])

    def open_metres(self):
        return [i for i, (m0, m1) in enumerate(self.metres)
                if not any(m0 < c1 and c0 < m1 for c0, c1 in self.keep_clear)]

    def add(self, cls, bulk, weights):
        """Spreads `bulk` m3 of class `cls` over the open metres by `weights` (a function of a metre's (a0, a1)),
        or evenly when they give nothing."""
        open_ = self.open_metres()
        w = [max(0.0, weights(*self.metres[i])) for i in open_]
        if sum(w) <= 0:
            w = [1.0] * len(open_)
        for i, wi in zip(open_, w):
            self.bulk[i][cls] = self.bulk[i].get(cls, 0.0) + bulk * wi / sum(w)

    def lay(self, depth):
        """The heap's metres: the foot's width so its deepest metre is `depth`, within HEAP_WIDTH_M; each metre as
        deep as its bulk needs over a triangle of that width (a metre under MIN_HEAP_M gives its bulk to the
        deepest). Each: (a0, a1, depth, width, slot, its bulk per class)."""
        totals = [sum(b.values()) for b in self.bulk]
        if max(totals, default=0.0) <= 0:
            return
        deepest = max(range(len(totals)), key=lambda i: totals[i] / (self.metres[i][1] - self.metres[i][0]))
        width = min(max(2 * totals[deepest] / (self.metres[deepest][1] - self.metres[deepest][0]) / max(depth, 1e-6),
                        HEAP_WIDTH_M[0]), HEAP_WIDTH_M[1])
        for i, ((m0, m1), total) in enumerate(zip(self.metres, totals)):
            if i != deepest and 0 < 2 * total / (m1 - m0) / width < MIN_HEAP_M:
                for cls, v in self.bulk[i].items():
                    self.bulk[deepest][cls] = self.bulk[deepest].get(cls, 0.0) + v
                self.bulk[i] = {}
        for (m0, m1), bulk in zip(self.metres, self.bulk):
            total = sum(bulk.values())
            if total > 0:
                self.heap.append((m0, m1, 2 * total / (m1 - m0) / width, width, HEAP_SLOT[max(bulk, key=bulk.get)],
                                  bulk))

    def top(self, a, d):
        """The heap's top over (a along, d out), or None off the heap: (height above y0, slope angle)."""
        for m0, m1, h, w, _, _ in self.heap:
            if m0 <= a <= m1 and 0.0 <= d <= w:
                return h * (1 - d / w), math.atan2(h, w)
        return None


def _steps(length):
    n = max(1, round(length / FOOT_STEP_M))
    return [length * k / n for k in range(n)] if n > 1 else [0.0]


class Rubble:
    """The rubble being laid: its meshes per LOD (LOD0 and LOD1 get the pieces, LOD0-2 the heaps), collision,
    snags, and the volumes per class (removed solid, pieces' solid, heaps' bulk) and piece counts it reports."""

    def __init__(self):
        self.lods = [Mesh(), Mesh(), Mesh()]
        self.collision, self.snag = [], []
        self.removed, self.pieces, self.core, self.counts = {}, {}, {}, {}
        self.spread_log = []

    def note(self, cls, removed=0.0, pieces=0.0, core=0.0, count=0):
        for table, v in ((self.removed, removed), (self.pieces, pieces), (self.core, core)):
            table[cls] = table.get(cls, 0.0) + v
        if count:
            self.counts[cls] = self.counts.get(cls, 0) + count

    def place(self, piece, rigid, cls):
        """A kit piece placed by `rigid`, its meshes, shapes and snags, slots renamed for the house."""
        for mesh, lod in zip(self.lods[:2], piece["lods"]):
            kits_vehicle.append_mesh(mesh, lod, rigid, PIECE_SLOTS)
        self.collision += [kits_vehicle.transform_shape(c, rigid) for c in piece["collision"]]
        self.snag += [kits_vehicle.transform_snag(s, rigid) for s in piece["snag"]]
        self.note(cls, pieces=piece["volume_m3"], count=1)

    def heap(self, foot):
        """A foot's heap as its sloped boxes (masonry) and their faces: each metre's top, the step where a deeper
        metre stands above its neighbour, and the run's ends."""
        heap = foot.heap
        for i, (m0, m1, h, w, slot, bulk) in enumerate(heap):
            beta = math.atan2(h, w)
            length = math.hypot(h, w)
            thick = h * w / length + 0.05  # from the top down to below the ground at the wall's face (derived)
            ex = (foot.along[0], 0.0, foot.along[1])
            ey = (foot.out[0] * math.sin(beta), math.cos(beta), foot.out[1] * math.sin(beta))  # the slope's normal
            ez = prism.cross(ex, ey)
            down = (foot.out[0] * math.cos(beta), -math.sin(beta), foot.out[1] * math.cos(beta))
            z0, z1 = sorted((0.0, prism.dot(ez, down) * length))
            frame = prism.Frame(foot.point(m0, 0.0, foot.y0 + h), ex, ey, ez)
            self.collision.append(prism.Member(frame, (0.0, -thick, z0), (m1 - m0, 0.0, z1)).box("masonry"))
            for mesh in self.lods:
                top = [foot.point(m0, 0.0, foot.y0 + h), foot.point(m1, 0.0, foot.y0 + h),
                       foot.point(m1, w, foot.y0), foot.point(m0, w, foot.y0)]
                mesh.face([mesh.vertex(p) for p in top], ey, slot, [_uv(p, ey) for p in top])
                for a, sign, nb in ((m0, -1.0, heap[i - 1] if i and abs(heap[i - 1][1] - m0) < 1e-9 else None),
                                    (m1, 1.0, heap[i + 1] if i + 1 < len(heap) and abs(heap[i + 1][0] - m1) < 1e-9
                                     else None)):
                    low = nb[2] if nb else 0.0
                    if h - low <= 1e-9:
                        continue
                    tri = [foot.point(a, 0.0, foot.y0 + low), foot.point(a, 0.0, foot.y0 + h), foot.point(a, w, foot.y0)]
                    normal = (sign * foot.along[0], 0.0, sign * foot.along[1])
                    mesh.face([mesh.vertex(p) for p in tri], normal, slot, [_uv(p, normal) for p in tri])
            built, total = (m1 - m0) * h * w / 2, sum(bulk.values())
            for cls, v in bulk.items():
                self.note(cls, core=built * v / total)

    def layer(self, rect, y0, h, shares):
        """A flat layer of rubble over a floor rect (x0, x1, z0, z1), h deep, its bulk split by `shares` per class:
        one box from just under the floor and its top and sides, in its main class's slot."""
        x0, x1, z0, z1 = rect
        self.collision.append(prism.axis_box((x0, y0 - 0.05, z0), (x1, y0 + h, z1), "masonry"))
        frame = prism.Frame((0.0, 0.0, 0.0), (1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0))
        slot = HEAP_SLOT[max(shares, key=shares.get)]
        for mesh in self.lods[:2]:
            for pts, normal, kind in prism.Member(frame, (x0, y0, z0), (x1, y0 + h, z1)).faces:
                if kind != "y-":
                    mesh.face([mesh.vertex(p) for p in pts], normal, slot, [_uv(p, normal) for p in pts])
        for cls, share in shares.items():
            self.note(cls, core=(x1 - x0) * (z1 - z0) * h * share)


def _uv(p, normal):
    return uv_upright(p, normal)


def along(rng, n, where):
    """n places along a foot, stratified by `where`, [(a0, a1, weight)]: piece i at the weights' quantile
    (i + u) / n, shuffled."""
    total = sum(w for _, _, w in where)
    out = []
    for i in range(n):
        q, acc = (i + rng.random()) / n * total, 0.0
        for a0, a1, w in where:
            if acc + w >= q or (a0, a1, w) == where[-1]:
                out.append(a0 + (a1 - a0) * min(1.0, max(0.0, (q - acc) / w if w else 0.5)))
                break
            acc += w
    rng.shuffle(out)
    return out


def scatter(rubble, foot, cls, pieces, where, rng, lean_along=False):
    """Lays kit pieces of class `cls` from `foot` by the spread rule: along it by `where` (along()), out from its
    wall by exp(-d / λ) of their class, LEE_WIDEN times wider in the share `foot.lee`, each clear of the wall by its
    own reach, lying on the heap's slope or the ground at a seeded yaw (a member within 30 degrees of the wall's
    line when `lean_along`). Logs each (foot, class, d, λ, call) in rubble.spread_log, `call` counting the scatters
    (each one stratified on its own)."""
    lam = LAMBDA_M[cls] * (1 + (LEE_WIDEN - 1) * foot.lee)
    call = len({entry[4] for entry in rubble.spread_log})
    up_out = (foot.out[1], 0.0, -foot.out[0])  # up x out: tilts a piece's up towards the slope's fall
    for piece, d, a in zip(pieces, spread(rng, len(pieces), lam), along(rng, len(pieces), where)):
        r = reach(piece)
        rubble.spread_log.append((foot.name, cls, d, lam, call))
        d = max(d, r)
        a = min(max(a, r), foot.length - r) if foot.length > 2 * r else foot.length / 2
        wall_yaw = math.atan2(foot.along[1], foot.along[0])
        yaw = -wall_yaw + rng.uniform(-math.pi / 6, math.pi / 6) if lean_along else rng.uniform(0, 2 * math.pi)
        top = foot.top(a, d)
        x, _, z = foot.point(a, d)
        if top:
            rubble.place(piece, rest(piece, x, z, foot.y0 + top[0], yaw, top[1], up_out), cls)
        else:
            rubble.place(piece, rest(piece, x, z, foot.y0, yaw), cls)


def rest(piece, x, z, y, yaw, tilt=0.0, tilt_axis=(1.0, 0.0, 0.0)):
    """The Rigid that lays a kit piece (its frame: lying on y = 0, centred in plan) turned by `yaw` about up, tilted
    by `tilt` about the horizontal `tilt_axis` (a heap's slope), with its centre's underside at (x, y, z)."""
    c, s = math.cos(yaw), math.sin(yaw)
    turn = kits_vehicle.Rigid(((c, 0.0, s), (0.0, 1.0, 0.0), (-s, 0.0, c)))
    lean = kits_vehicle.axis_rotation((0.0, 0.0, 0.0), tilt_axis, tilt) if tilt else kits_vehicle.Rigid()
    return turn.then(lean).then(kits_vehicle.translation((x, y, z)))


def reach(piece):
    """How far a piece reaches from its centre in plan (half its footprint's diagonal)."""
    xs = [v[0] for v in piece["lods"][0].vertices]
    zs = [v[2] for v in piece["lods"][0].vertices]
    return math.hypot(max(abs(min(xs)), abs(max(xs))), max(abs(min(zs)), abs(max(zs))))


# --- the pieces --------------------------------------------------------------------------------------------------

def tile_piece(rng, tile, size):
    """A shard of `size`, or (WHOLE_SHARE of them) a whole tile, its clay in a seeded cell."""
    cell = rng.randrange(len(debris_tile.CELLS_M))
    if rng.random() < WHOLE_SHARE:
        return debris_tile.whole(tile, cell)
    return debris_tile.shard(rng, tile, min(size, tile.length - 2 * debris_tile.MARGIN_M), rng.randint(2, 6), cell)


def adobe_piece(rng, size):
    """An adobe chunk: a block (the sheet's 400 x 200 x 250 mm, broken down to `size` along its longest side) with
    crumbled arrises, its faces in metre UVs from a seeded offset (recipe adobe), slot "brick" of debris_brick."""
    length = size
    height = min(0.2, size * rng.uniform(0.4, 0.8))
    width = min(0.25, size * rng.uniform(0.5, 0.9))
    chamfer = {c: rng.uniform(*ADOBE_CHAMFER_M) * min(1.0, height / 0.1) for c in debris_brick.CORNERS}
    off = (rng.uniform(0, 1.5), rng.uniform(0, 1.5))
    lods = [debris_brick.brick_mesh((length, height, width), chamfer), debris_brick.brick_mesh((length, height, width))]
    for mesh in lods:  # metre UVs on the adobe set, not the brick's cell
        mesh.faces = [(idx, n, [tuple(a + b for a, b in zip(uv_upright(mesh.vertices[i], n), off)) for i in idx], slot)
                      for idx, n, _, slot in mesh.faces]
    box = {"shape": "box", "center": [0.0, height / 2, 0.0], "size": [length, height, width],
           "rotation": [0.0, 0.0, 0.0, 1.0], "material": "masonry", "landable": False}
    return {"lods": lods, "collision": [box], "snag": [], "volume_m3": length * height * width}


def clay_slab(rng, size, thickness):
    """A slab of fallen ceiling clay: a jagged outline (lib.debris_tile's shard outline of a slab its size), its
    thickness, metre UVs (recipe clay_render), one box; slot "clay"."""
    parent = debris_tile.Tile(size * 1.25, size * 1.0, thickness, 0.0)
    points, _, _, (x0, z0, x1, z1) = debris_tile.shard_outline(rng, parent, size, rng.randint(3, 5))
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    pts = [(x - cx, z - cz) for x, z in points]
    mesh = Mesh()
    n = len(pts)
    ax_, az_ = debris_tile._area_centroid(pts)
    for y, normal in ((thickness, (0.0, 1.0, 0.0)), (0.0, (0.0, -1.0, 0.0))):  # each face a fan from its centroid
        centre = mesh.vertex((ax_, y, az_))
        ring = [mesh.vertex((x, y, z)) for x, z in pts]
        for i in range(n):
            mesh.face([centre, ring[i], ring[(i + 1) % n]], normal, "clay")
    top, bottom = list(range(1, n + 1)), list(range(n + 2, 2 * n + 2))  # the two rings, after each fan's centre
    for i in range(n):
        (ax, az), (bx, bz) = pts[i], pts[(i + 1) % n]
        length = math.hypot(bx - ax, bz - az)
        mesh.face([bottom[i], bottom[(i + 1) % n], top[(i + 1) % n], top[i]], ((bz - az) / length, 0.0, -(bx - ax) / length),
                  "clay")
    box = {"shape": "box", "center": [0.0, thickness / 2, 0.0], "size": [x1 - x0, thickness, z1 - z0],
           "rotation": [0.0, 0.0, 0.0, 1.0], "material": "masonry", "landable": False}
    area = abs(sum(x0_ * z1_ - x1_ * z0_ for (x0_, z0_), (x1_, z1_) in zip(pts, pts[1:] + pts[:1]))) / 2
    return {"lods": [mesh, mesh], "collision": [box], "snag": [], "volume_m3": area * thickness}


def timber_piece(rng, section, length, break_ends, nails=1, splinter_len=FALLEN_SPLINTER_M):
    """A fallen member as K03's piece: lying on its wider face, broken at `break_ends` ends."""
    w, h = max(section), min(section)
    splinter = min(splinter_len, (length - 0.1) / max(1, break_ends) / 2)
    nails = nails if length - break_ends * splinter >= 0.3 else 0
    return debris_timber.piece(rng, (w, h), length, break_ends, splinter, TIMBER_SPLINTERS if break_ends else 0,
                               nails, 0.05 if nails else 0.0)
