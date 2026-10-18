"""Stem skeletons for the vegetation generators (p1-v13-shrub, design decision 1; V01 grows the same module):
`count` stems rising from a base disc, each a tapered, arching tube that branches twice above `bare_base` into
twigs, and the tube meshes of both per LOD. Plain Python in asset space (lib.frame: +Y up, metres), drawing from the
caller's seeded random.Random.

A stem is the quadratic curve p(t) = base + (0, L t, 0) + lean * arch * L * t^2 for t in [0, 1]: its tip has
drifted arch * L horizontally (the sheet's arch, the tip's drift as a share of height) along `lean`, a horizontal
direction outward from the base disc. Its radius tapers from `stem_r` at the base to STEM_TIP_R (a 10 mm stem) at
the tip; thinner wood is a twig. RINGS + 1 samples of the curve carry the LOD0 tube; the chords between every
(RINGS / SEGMENTS)-th sample are the stem's straight `segments`, which carry the LOD1 tube and which the generator
turns into capsules and hooks, so mesh and collision come from the same points. A chord's sag off the curve is
arch * L / (4 SEGMENTS^2): 11 mm on a 2 m stem at arch 0.2, under the audit's 50 mm (design: risks).

Twigs: a first-order twig leaves a stem at each node, NODE_SPACING_M apart along it above `bare_base`, tilted
from the stem's tangent by the fork angle (the sheet's fork_angle_deg; FORK_DEG unless the caller gives one) at
any azimuth about the stem (AZIMUTH_JITTER), and reaching FIRST_REACH of the way to the clump's envelope along its
direction; two second-order twigs fork from its end, spread by SPREAD_DEG, and reach the envelope with an outward
bias (1 - 0.3 u^2 of the way). The envelope is the caller's `reach(p, d)`: the distance from p along the unit
direction d to the clump's surface, 0 when p lies outside. Radii TWIG1_R 8 -> 5 mm and TWIG2_R 5 -> 3 mm, the
sheet's 3-8 mm twigs.

Tubes (`tubes`): stems STEM_SIDES[lod]-sided, 6 at LOD0 over the rings and 4 at LOD1 over the segment ends, the tip
closed by a cap; twigs TWIG_SIDES-sided (3) at LOD0 only; nothing from LOD2 on. The rings of a tube share a
parallel-transported frame, so it does not twist. UVs are metres along and around the member, V up the member
(AST-07: the bark recipe tiles at 1 m).
"""
import math

STEM_TIP_R = 0.005
RINGS = 6
SEGMENTS = 3
NODE_SPACING_M = 0.2
NODE_JITTER = (0.3, 0.7)        # a node's place within its spacing slot
LEAN_JITTER = 0.7               # radians about the outward azimuth, for a stem's drift direction
AZIMUTH_JITTER = math.pi        # radians about the outward azimuth, for a first-order twig: any side of its stem
                                # (1.0 left the crown's core empty, a hole into the clump from 20 deg above)
FORK_DEG = (30.0, 50.0)         # the sheet's fork_angle_deg default
SPREAD_DEG = (20.0, 40.0)
PITCH_JITTER = 0.25             # added to a second-order direction's Y before renormalising
FIRST_REACH = (0.45, 0.65)
MIN_TWIG_M = 0.05
TWIG1_R = (0.004, 0.0025)
TWIG2_R = (0.0025, 0.0015)
STEM_SIDES = {0: 6, 1: 4}
TWIG_SIDES = 3
AXES = ((1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0))


class Stem:
    """One stem: `rings` [(point, radius)] from base to tip and `segments` [(p0, p1, r0, r1)], the straight pieces
    between every (RINGS / SEGMENTS)-th ring."""

    def __init__(self, base, length, lean, arch, radius0):
        self.base, self.length, self.lean, self.arch, self.radius0 = base, length, lean, arch, radius0
        self.rings = [(self.point(k / RINGS), self.radius(k / RINGS)) for k in range(RINGS + 1)]
        ends = self.rings[::RINGS // SEGMENTS]
        self.segments = [(a[0], b[0], a[1], b[1]) for a, b in zip(ends, ends[1:])]

    def point(self, t):
        d = self.arch * self.length * t * t
        return (self.base[0] + self.lean[0] * d, self.base[1] + self.length * t, self.base[2] + self.lean[2] * d)

    def tangent(self, t):
        d = 2 * self.arch * self.length * t
        return normalize((self.lean[0] * d, self.length, self.lean[2] * d))

    def radius(self, t):
        return self.radius0 + (STEM_TIP_R - self.radius0) * t


class Twig:
    """A straight twig from p0 (radius r0) to p1 (radius r1); `level` 1 leaves a stem, 2 leaves a level-1 twig."""

    def __init__(self, p0, p1, r0, r1, level):
        self.p0, self.p1, self.r0, self.r1, self.level = p0, p1, r0, r1, level


def build(rng, count, base_r, lengths, stem_r, arch, bare_base, reach, fork_deg=FORK_DEG):
    """(stems, twigs): `count` stems on a disc of radius `base_r` at evenly spread, jittered azimuths, each of a
    length drawn from `lengths` (lo, hi), base radius `stem_r`, leaning outward by `arch`, with their twigs above
    `bare_base` grown out to `reach`, leaving their stems at an angle drawn from `fork_deg` (lo, hi) degrees."""
    stems = []
    for i in range(count):
        phi = 2 * math.pi * (i + rng.random()) / count
        rho = base_r * math.sqrt(rng.random())
        tilt = phi + rng.uniform(-LEAN_JITTER, LEAN_JITTER)
        stems.append(Stem((rho * math.cos(phi), 0.0, rho * math.sin(phi)), rng.uniform(*lengths),
                          (math.cos(tilt), 0.0, math.sin(tilt)), arch, stem_r))
    twigs = []
    for stem in stems:
        twigs += _twigs(rng, stem, bare_base, reach, fork_deg)
    return stems, twigs


def _twigs(rng, stem, bare_base, reach, fork_deg):
    twigs = []
    for k in range(int((stem.length - bare_base) / NODE_SPACING_M)):
        t = (bare_base + (k + rng.uniform(*NODE_JITTER)) * NODE_SPACING_M) / stem.length
        node, tangent = stem.point(t), stem.tangent(t)
        azimuth = math.atan2(node[2], node[0]) if math.hypot(node[0], node[2]) > 0.05 else rng.uniform(0, 2 * math.pi)
        azimuth += rng.uniform(-AZIMUTH_JITTER, AZIMUTH_JITTER)
        fork = math.radians(rng.uniform(*fork_deg))
        d = normalize(add(scale(tangent, math.cos(fork)), (math.cos(azimuth) * math.sin(fork), 0.0, math.sin(azimuth) * math.sin(fork))))
        distance = reach(node, d)
        if distance < MIN_TWIG_M:
            continue
        end = add(node, scale(d, rng.uniform(*FIRST_REACH) * distance))
        twigs.append(Twig(node, end, *TWIG1_R, 1))
        for side in (-1, 1):
            d2 = _rotate_y(d, side * math.radians(rng.uniform(*SPREAD_DEG)))
            d2 = normalize((d2[0], d2[1] + rng.uniform(-PITCH_JITTER, PITCH_JITTER), d2[2]))
            distance = reach(end, d2)
            u = rng.random()
            if distance < MIN_TWIG_M:
                continue
            twigs.append(Twig(end, add(end, scale(d2, (1 - 0.3 * u * u) * distance)), *TWIG2_R, 2))
    return twigs


def tubes(mesh, stems, twigs, lod, stem_slot, twig_slot):
    """Adds the skeleton's tubes for `lod` to a lib.geometry.Mesh: stems STEM_SIDES[lod]-sided over their rings
    (LOD0) or their segment ends (LOD1), capped at the tip; twigs TWIG_SIDES-sided at LOD0. Nothing from LOD2 on."""
    sides = STEM_SIDES.get(lod)
    if sides is None:
        return
    step = 1 if lod == 0 else RINGS // SEGMENTS
    for stem in stems:
        _tube(mesh, stem.rings[::step], sides, stem_slot, cap=True,
              tangents=[stem.tangent(k / RINGS) for k in range(0, RINGS + 1, step)])
    if lod == 0:
        for twig in twigs:
            _tube(mesh, [(twig.p0, twig.r0), (twig.p1, twig.r1)], TWIG_SIDES, twig_slot, cap=False)


def _tube(mesh, rings, sides, slot, cap, tangents=None):
    """A tube over [(point, radius)] rings, each ring perpendicular to its tangent (given, or the chord through its
    neighbours): one quad per side and band with its outward normal and metre UVs (U around, V along), and a
    polygon cap over the last ring when `cap`."""
    points = [p for p, _ in rings]
    last = len(points) - 1
    if tangents is None:
        tangents = [normalize(sub(points[min(i + 1, last)], points[max(i - 1, 0)])) for i in range(len(points))]
    u = normalize(cross(tangents[0], _helper(tangents[0])))
    ring_ids, along = [], [0.0]
    for i, ((p, r), tangent) in enumerate(zip(rings, tangents)):
        if i:
            u = normalize(sub(u, scale(tangent, dot(u, tangent))))  # parallel transport, no twist
            along.append(along[-1] + norm(sub(p, points[i - 1])))
        v = cross(tangent, u)
        ring_ids.append([mesh.vertex(add(p, add(scale(u, r * math.cos(a)), scale(v, r * math.sin(a)))))
                         for a in (2 * math.pi * j / sides for j in range(sides))])
    for i in range(last):
        (p0, r0), (p1, r1) = rings[i], rings[i + 1]
        axis_mid = scale(add(p0, p1), 0.5)
        for j in range(sides):
            k = (j + 1) % sides
            quad = [ring_ids[i][j], ring_ids[i][k], ring_ids[i + 1][k], ring_ids[i + 1][j]]
            corners = [mesh.vertices[q] for q in quad]
            normal = normalize(_newell(corners))
            centre = scale(add(add(corners[0], corners[1]), add(corners[2], corners[3])), 0.25)
            if dot(normal, sub(centre, axis_mid)) < 0:
                normal = scale(normal, -1.0)
            c0, c1 = 2 * math.pi * r0 / sides, 2 * math.pi * r1 / sides
            mesh.face(quad, normal, slot, [(j * c0, along[i]), ((j + 1) * c0, along[i]),
                                           ((j + 1) * c1, along[i + 1]), (j * c1, along[i + 1])])
    if cap:
        mesh.face(ring_ids[last], tangents[last], slot)


def _helper(t):
    """The world axis least aligned with t, to seed a frame perpendicular to it."""
    return AXES[min(range(3), key=lambda a: abs(t[a]))]


def _rotate_y(v, angle):
    c, s = math.cos(angle), math.sin(angle)
    return (c * v[0] + s * v[2], v[1], -s * v[0] + c * v[2])


def _newell(points):
    n = [0.0, 0.0, 0.0]
    for a, b in zip(points, points[1:] + points[:1]):
        n[0] += (a[1] - b[1]) * (a[2] + b[2])
        n[1] += (a[2] - b[2]) * (a[0] + b[0])
        n[2] += (a[0] - b[0]) * (a[1] + b[1])
    return n


def add(a, b):
    return (a[0] + b[0], a[1] + b[1], a[2] + b[2])


def sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def scale(a, k):
    return (a[0] * k, a[1] * k, a[2] * k)


def dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def norm(a):
    return math.sqrt(dot(a, a))


def normalize(a):
    length = norm(a)
    if length < 1e-12:
        raise ValueError("a zero vector has no direction")
    return (a[0] / length, a[1] / length, a[2] / length)
