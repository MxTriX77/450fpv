"""The vehicle state kits' geometry (p1-r01-vaz-sedan, design decisions 3-5 and 7; object sheet
docs/object-sheets/vehicles.md, "How vehicles fail"): plain Python helpers the `vehicle` generator calls, with no
Blender and no import of the generator (a body is duck-typed: s, w and y as generators.vehicle.Body names them).

- Rigid transforms of meshes and collision shapes (the A lean about a diagonal, the F drop on the rims).
- The burst: its direction from `burst_dir`, the side it holes and the end it strikes.
- Fragment holes: seeded sizes and places on the burst side's flank, and the patch that cuts one from a lofted
  sheet: the sheet's quads round the hole are removed (lib.loft.sheet's `seams`, so no edge strip shows there) and a
  ring is stitched from their border to the hole's jagged rim, double-sided with its edge, its halo in the rust slot,
  and petals of the torn sheet bent inward for the large ones.
- Placement of glass crumbs and of the K07 scatter (1/r^2 areal density).

Numbers are the sheet's (measured M, published) or labelled assumed / derived where this module adds them.
"""
import math
import random

# --- the sheet's and the design's numbers ------------------------------------------------------------------------
LARGE_MIN_M, LARGE_MAX_M = 0.08, 0.18   # seeded large holes (measured M: 0.09, 0.15-0.18; the struck corner's
                                        # 0.20 x 0.25 m lamp tear is the sheet's largest and is built apart)
LARGE_SHARE = 6 / 16                    # measured M: about 6 large and 10 small holes
SMALL_MIN_M = 0.01                      # the sheet: small holes 10-30 mm
CUT_MIN_M = 0.03                        # the sheet: holes of 30 mm or more are cut in the mesh
PETAL_DEPTH_M = (0.010, 0.030)          # measured M: petals bent inward 10-30 mm
PETALS = (5, 7)                         # petals per large hole (assumed: a fragment's radial tears)
PETAL_GAP_DEG = (6.0, 14.0)             # the torn slit between two petals at the rim (assumed)
APERTURE_M = 0.045                      # derived: the open centre's reach from the petals' tips, so the panel's
                                        # collision, closed over a hole of 0.25 m or less (the sheet's stated
                                        # deviation), stays within AST-05's 5 cm of the visual
RIM_JITTER = 0.12                       # the rim's radius varies +-12 % round the hole (assumed: a torn outline)
HALO_JITTER = 0.35                      # a halo's reach varies +-35 % round it (assumed: rust creeps unevenly)
PATCH_RING_M = 0.03                     # a cut hole's patch reaches at least this far past its rim
EDGE_SNAG_R_M = 0.0004                  # the sheet: a torn edge's radius, half the 0.8 mm gauge
ZONE_CLEAR_M = 0.32                     # derived: seeded holes keep their patch this far from the bonnet's and the boot
                                        # lid's landing faces (AST-05's 2 cm zone reaches 0.3 m), where a hole closed
                                        # in collision would stand off the visual by more than 2 cm
FLAT_DROP_M = 0.05                      # measured M: the flat tyre's corner drop (the sheet's range 0.04-0.08)
CORNERS = ("front_left", "front_right", "rear_left", "rear_right")


# --- rigid transforms ---------------------------------------------------------------------------------------------

class Rigid:
    """p -> R p + t, with R a rotation matrix (rows)."""

    def __init__(self, rows=((1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0)), t=(0.0, 0.0, 0.0)):
        self.R = tuple(tuple(float(c) for c in r) for r in rows)
        self.t = tuple(float(c) for c in t)

    def is_identity(self):
        return self.R == ((1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0)) and self.t == (0.0, 0.0, 0.0)

    def vector(self, v):
        return tuple(sum(self.R[i][k] * v[k] for k in range(3)) for i in range(3))

    def point(self, p):
        v = self.vector(p)
        return (v[0] + self.t[0], v[1] + self.t[1], v[2] + self.t[2])

    def quat(self, q):
        """The rotation q (a unit quaternion [x, y, z, w]) followed by this transform's."""
        return _quat_mul(_quat_from_rows(self.R), q)

    def then(self, other):
        """This transform followed by `other`."""
        rows = tuple(tuple(sum(other.R[i][k] * self.R[k][j] for k in range(3)) for j in range(3)) for i in range(3))
        return Rigid(rows, other.point(self.t))


def translation(t):
    return Rigid(t=t)


def axis_rotation(origin, axis, angle):
    """The rotation by `angle` (radians, right-handed about the unit `axis`) about the line through `origin`."""
    x, y, z = _unit(axis)
    c, s = math.cos(angle), math.sin(angle)
    k = 1 - c
    rows = ((c + x * x * k, x * y * k - z * s, x * z * k + y * s),
            (y * x * k + z * s, c + y * y * k, y * z * k - x * s),
            (z * x * k - y * s, z * y * k + x * s, c + z * z * k))
    rot = Rigid(rows)
    moved = rot.vector(origin)
    return Rigid(rows, (origin[0] - moved[0], origin[1] - moved[1], origin[2] - moved[2]))


def transform_range(mesh, vertices, faces, rigid):
    """Moves mesh vertices [v0, v1) and the normals of faces [f0, f1) by `rigid`, in place."""
    if rigid.is_identity():
        return
    for i in range(*vertices):
        mesh.vertices[i] = rigid.point(mesh.vertices[i])
        if i in mesh.normals:
            mesh.normals[i] = rigid.vector(mesh.normals[i])
    for k in range(*faces):
        indices, normal, uvs, slot = mesh.faces[k]
        mesh.faces[k] = (indices, rigid.vector(normal), uvs, slot)


def append_mesh(dst, src, rigid=None, slot_map=None):
    """Appends src's vertices and faces to dst, moved by `rigid`, slots renamed by `slot_map`."""
    rigid = rigid or Rigid()
    base = len(dst.vertices)
    for i, p in enumerate(src.vertices):
        dst.vertex(rigid.point(p), rigid.vector(src.normals[i]) if i in src.normals else None, src.uv2.get(i))
    for indices, normal, uvs, slot in src.faces:
        dst.faces.append(([base + i for i in indices], rigid.vector(normal), list(uvs),
                          (slot_map or {}).get(slot, slot)))


def transform_shape(shape, rigid):
    """A collision or wind shape moved by `rigid`."""
    out = dict(shape)
    out["center"] = list(rigid.point(shape["center"]))
    if "rotation" in shape:
        out["rotation"] = list(rigid.quat(shape["rotation"]))
    return out


def transform_snag(snag, rigid):
    out = dict(snag)
    out["points"] = [list(rigid.point(p)) for p in snag["points"]]
    return out


def _quat_from_rows(m):
    tr = m[0][0] + m[1][1] + m[2][2]
    if tr > 0:
        k = 0.5 / math.sqrt(tr + 1.0)
        q = ((m[2][1] - m[1][2]) * k, (m[0][2] - m[2][0]) * k, (m[1][0] - m[0][1]) * k, 0.25 / k)
    elif m[0][0] > m[1][1] and m[0][0] > m[2][2]:
        k = 2.0 * math.sqrt(1.0 + m[0][0] - m[1][1] - m[2][2])
        q = (0.25 * k, (m[0][1] + m[1][0]) / k, (m[0][2] + m[2][0]) / k, (m[2][1] - m[1][2]) / k)
    elif m[1][1] > m[2][2]:
        k = 2.0 * math.sqrt(1.0 + m[1][1] - m[0][0] - m[2][2])
        q = ((m[0][1] + m[1][0]) / k, 0.25 * k, (m[1][2] + m[2][1]) / k, (m[0][2] - m[2][0]) / k)
    else:
        k = 2.0 * math.sqrt(1.0 + m[2][2] - m[0][0] - m[1][1])
        q = ((m[0][2] + m[2][0]) / k, (m[1][2] + m[2][1]) / k, 0.25 * k, (m[1][0] - m[0][1]) / k)
    n = math.sqrt(sum(c * c for c in q))
    return tuple(c / n for c in q)


def _quat_mul(a, b):
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    q = (aw * bx + ax * bw + ay * bz - az * by, aw * by - ax * bz + ay * bw + az * bx,
         aw * bz + ax * by - ay * bx + az * bw, aw * bw - ax * bx - ay * by - az * bz)
    n = math.sqrt(sum(c * c for c in q))
    return [c / n for c in q]


def _unit(v):
    n = math.sqrt(sum(c * c for c in v))
    return tuple(c / n for c in v)


# --- the burst and the lean --------------------------------------------------------------------------------------

def burst_vector(azimuth_deg, elevation_deg=0.0):
    """The unit direction from the car toward where the burst came from: azimuth clockwise from the nose seen from
    above (0 ahead, 90 the right side, 180 behind, 270 the left side), elevation up from level. Asset space: the
    nose at -X, the right side +Z (generators.vehicle)."""
    a, e = math.radians(azimuth_deg), math.radians(elevation_deg)
    return (-math.cos(a) * math.cos(e), math.sin(e), math.sin(a) * math.cos(e))


def lean(hubs, flat, drop=FLAT_DROP_M):
    """The rigid rotation of the body about the diagonal opposite the flat corner (design decision 3), through the
    other diagonal's two hub centres, that lowers the flat corner's hub by `drop`. `hubs` maps the CORNERS to
    their hub centres."""
    other = {"front_left": ("front_right", "rear_left"), "rear_right": ("front_right", "rear_left"),
             "front_right": ("front_left", "rear_right"), "rear_left": ("front_left", "rear_right")}[flat]
    a, b = hubs[other[0]], hubs[other[1]]
    axis = _unit(tuple(b[k] - a[k] for k in range(3)))
    target = hubs[flat][1] - drop
    lo, hi = -0.3, 0.3
    if axis_rotation(a, axis, lo).point(hubs[flat])[1] < axis_rotation(a, axis, hi).point(hubs[flat])[1]:
        lo, hi = hi, lo
    for _ in range(80):  # bisection on the angle: the hub's height is monotonic over +-0.3 rad
        mid = (lo + hi) / 2
        if axis_rotation(a, axis, mid).point(hubs[flat])[1] > target:
            lo = mid
        else:
            hi = mid
    return axis_rotation(a, axis, (lo + hi) / 2)


# --- fragment holes ----------------------------------------------------------------------------------------------

class Hole:
    """One fragment hole on a flank: centre (s, y) on the side `side`, diameter d. A cut hole carries its jagged
    rim (radius per angle), its halo radius per angle and, when large, its petals (tip angle, the sector's
    angles, the tip's radius before bending and the tip's depth behind the skin)."""

    def __init__(self, side, s, y, d, halo, rng, cut=None, ellipse=None):
        self.side, self.s, self.y, self.d = side, s, y, d
        self.cut = d >= CUT_MIN_M if cut is None else cut
        self.large = d >= LARGE_MIN_M
        self.halo = halo
        self.ellipse = ellipse  # (semi-axis along s, semi-axis along y) for an elongated hole, else round
        self.phase = [rng.uniform(0, 2 * math.pi) for _ in range(6)]
        self.halo_phase = [rng.uniform(0, 2 * math.pi) for _ in range(6)]
        self.petals = []
        if self.large:
            n = rng.randint(*PETALS)
            start = rng.uniform(0, 2 * math.pi)
            depth = rng.uniform(*PETAL_DEPTH_M)
            for k in range(n):
                a0 = start + 2 * math.pi * k / n
                a1 = start + 2 * math.pi * (k + 1) / n
                gap = math.radians(rng.uniform(*PETAL_GAP_DEG)) / 2
                tip_angle = (a0 + a1) / 2 + rng.uniform(-0.15, 0.15) * (a1 - a0)
                self.petals.append({"a0": a0 + gap, "a1": a1 - gap, "tip": tip_angle,
                                    "depth": depth * rng.uniform(0.8, 1.0), "reach": rng.uniform(0.85, 1.0)})

    @property
    def r(self):
        return self.d / 2

    def rim_radius(self, a):
        base = self.r
        if self.ellipse:
            ra, rb = self.ellipse
            base = 1 / math.sqrt((math.cos(a) / ra) ** 2 + (math.sin(a) / rb) ** 2)
        return base * (1 + RIM_JITTER * _noise(a, self.phase))

    def patch_half(self):
        """The half-sizes (along s, along y) of the patch's rectangle."""
        if not self.ellipse:
            return self.patch_radius(), self.patch_radius()
        return tuple(e * (1 + RIM_JITTER) + PATCH_RING_M + 0.01 for e in self.ellipse)

    def halo_radius(self, a):
        """The rust halo's reach at angle a."""
        return self.r * self.halo * (1 + HALO_JITTER * _noise(a, self.halo_phase))

    def patch_radius(self):
        """The half-size of the square of sheet the hole's patch replaces: the rim's largest reach and a ring
        (the sheet's grid gets rows and levels on the square's sides)."""
        return self.r * (1 + RIM_JITTER) + PATCH_RING_M + 0.01

    def petal_tip(self, petal):
        """(radius of the tip before bending, the bend angle) of a petal: its length bent so the tip lies `depth`
        behind the skin, its tip as far in as the APERTURE_M rule asks (the open centre within 5 cm of a tip)."""
        rim = self.rim_radius(petal["tip"])
        depth = petal["depth"]
        reach = math.sqrt(max(APERTURE_M ** 2 - depth ** 2, 0.0)) * petal["reach"]
        reach = min(reach, 0.5 * rim + 0.01)
        length = rim - reach  # in plan, from the rim in to the tip
        bend = math.atan(depth / length)  # so the tip, length / cos(bend) along the bent sheet, lies `depth` behind
        # the bent tip lies at rim - length cos(bend) from the centre: move the flat tip in so it stays at `reach`
        flat = rim - (rim - reach) / math.cos(bend)
        return max(flat, 0.0), bend


def _noise(a, phase):
    """A smooth periodic value in [-1, 1] at angle a: harmonics with seeded phases, scaled by their amplitudes' sum."""
    return (0.45 * math.sin(a + phase[4]) + 0.4 * math.sin(2 * a + phase[0]) + 0.3 * math.sin(3 * a + phase[1])
            + 0.2 * math.sin(5 * a + phase[2]) + 0.15 * math.sin(7 * a + phase[3])
            + 0.1 * math.sin(11 * a + phase[5])) / 1.6


def sample_holes(n_large, n_small, small_max, halo, side, rng, region, height_bias=1.0):
    """Holes on one flank: `n_large` large (log-uniform LARGE_MIN_M..LARGE_MAX_M) and `n_small` small (log-uniform
    SMALL_MIN_M..small_max), placed by rejection: `region(s, y, radius)` says whether a hole's patch fits there (the
    flank band, clear of the arches, the door lines, the struck corner and the landing faces' zone), and
    `region.bounds` is (s_lo, s_hi, y_lo, y_hi). Heights are drawn as y = lo + (hi - lo) u^height_bias, so a bias
    above 1 keeps them low (a burst near the ground). Returns the holes, largest first."""
    # large: a power law between the bounds (u^2 in log size: few big tears, most near 0.1 m, as M's 0.09-0.18 m)
    sizes = [LARGE_MIN_M * (LARGE_MAX_M / LARGE_MIN_M) ** (rng.random() ** 2) for _ in range(n_large)]
    sizes += [math.exp(rng.uniform(math.log(SMALL_MIN_M), math.log(max(small_max, SMALL_MIN_M))))
              for _ in range(n_small)]
    sizes.sort(reverse=True)
    placed = []
    s_lo, s_hi, y_lo, y_hi = region.bounds
    for d in sizes:
        hole = Hole(side, 0.0, 0.0, d, halo, rng)
        radius = hole.patch_radius() if hole.cut else hole.r * halo
        for _ in range(4000):
            s = rng.uniform(s_lo, s_hi)
            y = y_lo + (y_hi - y_lo) * rng.random() ** height_bias
            if not region(s, y, radius):
                continue
            # squares (cut holes) or disks (mask-only) kept apart
            if any(max(abs(s - h.s), abs(y - h.y)) < radius + h.extent + 0.02 for h in placed):
                continue
            hole.s, hole.y = s, y
            break
        else:
            raise ValueError(f"no room on the burst side's panels for a {d:.3f} m hole: fewer holes or smaller")
        hole.extent = radius
        placed.append(hole)
    return placed


# --- the patch that cuts a hole from a lofted sheet --------------------------------------------------------------

def boundary_loop(quads):
    """The grid points (i, j) round the border of a simply connected set of quads, in order."""
    edges = {}
    for i, j in quads:
        for a, b, nb in (((i, j), (i + 1, j), (i, j - 1)), ((i + 1, j), (i + 1, j + 1), (i + 1, j)),
                         ((i + 1, j + 1), (i, j + 1), (i, j + 1)), ((i, j + 1), (i, j), (i - 1, j))):
            if nb not in quads:
                edges[a] = b
    start = min(edges)
    loop, p = [start], edges[start]
    while p != start:
        loop.append(p)
        p = edges[p]
        if len(loop) > len(edges):
            raise ValueError("a hole's removed quads are not simply connected")
    return loop


def emit_patch(mesh, hole, outer, surf, uv, thickness, slots, rim_points=48, petals=True):
    """Stitches the patch of `hole` into `mesh`: from `outer` (the border loop of the removed quads, as
    [(point, (s, y), uv)]) to the hole's jagged rim in slots["paint"], the rim's edge strip and, when `thickness`
    > 0, the inner surface; and the large hole's petals in slots["torn"]. `surf(s, y)` gives (point, outward unit
    normal) on the sheet, `uv(s, y)` its UV. Returns the edge snags (the rim's closed polyline and each petal's
    torn outline)."""
    c = (hole.s, hole.y)

    def angle(q):
        return math.atan2(q[1] - c[1], q[0] - c[0])

    # the outer loop counter-clockwise in (s, y), starting at its smallest angle
    if _signed_area([q for _, q, _ in outer]) < 0:
        outer = outer[::-1]
    k0 = min(range(len(outer)), key=lambda k: angle(outer[k][1]))
    outer = outer[k0:] + outer[:k0]
    a_out = _unwrap([angle(q) for _, q, _ in outer])
    angles = [a_out[0] + 1e-6 + 2 * math.pi * k / rim_points for k in range(rim_points)]
    rim = [(c[0] + hole.rim_radius(a) * math.cos(a), c[1] + hole.rim_radius(a) * math.sin(a)) for a in angles]
    front = _Sheet(mesh, surf, uv, thickness)
    outer_ids = [front.vertex_at(p, q, t) for p, q, t in outer]
    rim_ids = [front.vertex(q) for q in rim]
    # zipper: the outer loop to the rim, both counter-clockwise from the same angle
    i = k = 0
    n, m = len(outer), rim_points
    while i < n or k < m:
        next_o = a_out[i + 1] if i + 1 < n else a_out[0] + 2 * math.pi
        next_r = angles[k + 1] if k + 1 < m else angles[0] + 2 * math.pi
        if i < n and (k >= m or next_o <= next_r):
            front.tri(outer_ids[i], outer_ids[(i + 1) % n], rim_ids[k % m], slots["paint"])
            i += 1
        else:
            front.tri(outer_ids[i % n], rim_ids[(k + 1) % m], rim_ids[k % m], slots["paint"])
            k += 1
    front.rim_edge(rim_ids, c, slots["torn"])
    snags = [{"kind": "edge", "points": [list(front.points[v]) for v in rim_ids + rim_ids[:1]],
              "radius_m": EDGE_SNAG_R_M}]
    if petals and hole.large:
        for petal in hole.petals:
            snags.append(_petal(mesh, hole, petal, surf, uv, thickness, slots["torn"]))
    return snags


DECAL_OFFSET_M = 0.0005   # a halo decal stands this far proud of its panel (LOD0 only: under 20 m the depth buffer
                          # parts it from the paint)


def emit_decal(mesh, hole, surf, uv, slots, points=32):
    """The hole's rust halo as a decal (the sheet: halos are decal or mask, no physical role): a ring from just
    outside the rim to the jagged halo, DECAL_OFFSET_M proud of the sheet, in slots["halo"]; for a hole too small
    to cut (under CUT_MIN_M) also its dark opening, a disc in slots["dark"]."""
    c = (hole.s, hole.y)
    ring_in, ring_out, centre = [], [], None

    def at(q):
        p, n = surf(*q)
        return mesh.vertex(tuple(p[k] + DECAL_OFFSET_M * n[k] for k in range(3))), n

    inner_r = (lambda a: hole.rim_radius(a) * 1.02) if hole.cut else (lambda a: hole.r * (1 + 0.5 * RIM_JITTER *
                                                                                             _noise(a, hole.phase)))
    for k in range(points):
        a = 2 * math.pi * k / points
        q_in = (c[0] + inner_r(a) * math.cos(a), c[1] + inner_r(a) * math.sin(a))
        q_out = (c[0] + hole.halo_radius(a) * math.cos(a), c[1] + hole.halo_radius(a) * math.sin(a))
        ring_in.append((at(q_in), uv(*q_in)))
        ring_out.append((at(q_out), uv(*q_out)))
    for k in range(points):
        a, b = ring_in[k], ring_in[(k + 1) % points]
        d, e = ring_out[k], ring_out[(k + 1) % points]
        mesh.face([a[0][0], b[0][0], e[0][0], d[0][0]], a[0][1], slots["halo"], [a[1], b[1], e[1], d[1]])
    if not hole.cut:
        (centre, n) = at(c)
        for k in range(points):
            a, b = ring_in[k], ring_in[(k + 1) % points]
            mesh.face([centre, a[0][0], b[0][0]], n, slots["dark"], [uv(*c), a[1], b[1]])


class _Sheet:
    """Faces of a patch on a sheet: the outer surface and, with a thickness, the inner one offset along -normal."""

    def __init__(self, mesh, surf, uv, thickness):
        self.mesh, self.surf, self.uv, self.t = mesh, surf, uv, thickness
        self.points, self.normals, self.uvs, self.inner = {}, {}, {}, {}

    def vertex_at(self, p, q, uv):
        _, n = self.surf(*q)
        return self._add(p, n, uv)

    def vertex(self, q):
        p, n = self.surf(*q)
        return self._add(p, n, self.uv(*q))

    def _add(self, p, n, uv):
        v = self.mesh.vertex(p)
        self.points[v], self.normals[v], self.uvs[v] = p, n, uv
        if self.t > 0:
            self.inner[v] = self.mesh.vertex(tuple(p[k] - self.t * n[k] for k in range(3)))
        return v

    def _face(self, ids, slot):
        pts = [self.points[v] for v in ids]
        if len({tuple(round(c, 9) for c in p) for p in pts}) < len(pts) or _area(pts) < 1e-12:
            return
        n = _mean([self.normals[v] for v in ids])
        self.mesh.face(ids, n, slot, [self.uvs[v] for v in ids])
        if self.t > 0:
            self.mesh.face([self.inner[v] for v in ids], tuple(-c for c in n), slot, [self.uvs[v] for v in ids])

    def tri(self, a, b, c, slot):
        self._face([a, b, c], slot)

    def quad(self, a, b, c, d, slot):
        self._face([a, b, c, d], slot)

    def rim_edge(self, rim, centre, slot):
        """The edge strip round the rim, facing the hole's centre."""
        if self.t <= 0:
            return
        for k in range(len(rim)):
            a, b = rim[k], rim[(k + 1) % len(rim)]
            pa, pb = self.points[a], self.points[b]
            mid_n = _mean([self.normals[a], self.normals[b]])
            along = tuple(pb[i] - pa[i] for i in range(3))
            inward = _cross(mid_n, along)  # in the sheet's plane, across the rim
            mid = tuple((pa[i] + pb[i]) / 2 for i in range(3))
            centre_p, _ = self.surf(*centre)
            if sum(inward[i] * (centre_p[i] - mid[i]) for i in range(3)) < 0:
                inward = tuple(-x for x in inward)
            ua, ub = self.uvs[a], self.uvs[b]
            self.mesh.face([a, b, self.inner[b], self.inner[a]], _unit(inward), slot,
                           [ua, ub, (ub[0], ub[1] - self.t), (ua[0], ua[1] - self.t)])


def _petal(mesh, hole, petal, surf, uv, thickness, slot):
    """One petal: the sheet of its sector between the rim and its tip, bent about the rim inward (into the body,
    along -normal) so its tip lies petal["depth"] behind the skin. Returns its edge snag (side, tip, side)."""
    from .loft import sheet  # the double-sided sheet with its edges (a petal is a small lofted grid)
    flat_tip, bend = hole.petal_tip(petal)
    c = (hole.s, hole.y)
    tip_q = (c[0] + flat_tip * math.cos(petal["tip"]), c[1] + flat_tip * math.sin(petal["tip"]))
    base_angles = [petal["a0"] + (petal["a1"] - petal["a0"]) * k / 4 for k in range(5)]
    base = [(c[0] + hole.rim_radius(a) * math.cos(a), c[1] + hole.rim_radius(a) * math.sin(a)) for a in base_angles]
    rows, uvs = [], []
    steps = 4
    for r in range(steps + 1):
        f = r / steps
        row, row_uv = [], []
        for q in base:
            flat = (q[0] + (tip_q[0] - q[0]) * f, q[1] + (tip_q[1] - q[1]) * f)
            # bent: in the plane the point comes in by its run times cos(bend), and goes in by its run times sin
            run = math.hypot(flat[0] - q[0], flat[1] - q[1])
            k = math.cos(bend)
            plane = (q[0] + (flat[0] - q[0]) * k, q[1] + (flat[1] - q[1]) * k)
            p, n = surf(*plane)
            depth = run * math.sin(bend)
            row.append(tuple(p[i] - depth * n[i] for i in range(3)))
            row_uv.append(uv(*flat))
        rows.append(row)
        uvs.append(row_uv)
    _, n_mid = surf(*c)
    sheet(mesh, rows, uvs, slot, lambda p, n=n_mid: n, thickness)
    outline = [rows[0][0]] + [rows[r][0] for r in range(1, steps + 1)] + [rows[r][-1] for r in range(steps - 1, -1, -1)]
    return {"kind": "edge", "points": [list(p) for p in outline], "radius_m": EDGE_SNAG_R_M}


def _signed_area(points):
    return 0.5 * sum(a[0] * b[1] - b[0] * a[1] for a, b in zip(points, points[1:] + points[:1]))


def _unwrap(angles):
    out = [angles[0]]
    for a in angles[1:]:
        while a < out[-1]:
            a += 2 * math.pi
        while a - out[-1] > 2 * math.pi:
            a -= 2 * math.pi
        out.append(a)
    return out


def _mean(vectors):
    s = [sum(v[k] for v in vectors) for k in range(3)]
    return _unit(s)


def _cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def _area(pts):
    n = [0.0, 0.0, 0.0]
    for a, b in zip(pts, pts[1:] + pts[:1]):
        n[0] += (a[1] - b[1]) * (a[2] + b[2])
        n[1] += (a[2] - b[2]) * (a[0] + b[0])
        n[2] += (a[0] - b[0]) * (a[1] + b[1])
    return math.sqrt(sum(c * c for c in n)) / 2


# --- scatter -------------------------------------------------------------------------------------------------------

def scatter_radius(rng, r_min, r_max, falloff=2):
    """A distance from the source with areal density r^-falloff between r_min and r_max (the sheet's 1/r^2: the
    distance is then log-uniform)."""
    u = rng.random()
    if falloff == 2:
        return r_min * (r_max / r_min) ** u
    k = 2 - falloff
    return (r_min ** k + u * (r_max ** k - r_min ** k)) ** (1 / k)


def crumb_box(mesh, centre, size, yaw, slot, uv):
    """One glass crumb: a box of size (x, y, z) about `centre`, turned by `yaw` about the vertical, every corner UV'd
    to `uv` (a palette column)."""
    c, s = math.cos(yaw), math.sin(yaw)
    axes = ((c, 0.0, s), (0.0, 1.0, 0.0), (-s, 0.0, c))
    h = [v / 2 for v in size]
    ids = {}
    for sx in (-1, 1):
        for sy in (-1, 1):
            for sz in (-1, 1):
                p = tuple(centre[k] + sx * h[0] * axes[0][k] + sy * h[1] * axes[1][k] + sz * h[2] * axes[2][k]
                          for k in range(3))
                ids[sx, sy, sz] = mesh.vertex(p)
    for axis in range(3):
        for sign in (-1, 1):
            keys = [k for k in ids if k[axis] == sign]
            o = [(axis + 1) % 3, (axis + 2) % 3]
            keys.sort(key=lambda k: math.atan2(k[o[1]], k[o[0]]))
            n = tuple(sign * axes[axis][k] for k in range(3))
            mesh.face([ids[k] for k in keys], n, slot, [uv] * 4)


def rng_for(params, what):
    """A seeded stream per kit item, so one item's draws never shift another's."""
    return random.Random(f"vehicle {params['model']} {params['seed']} {what}")
