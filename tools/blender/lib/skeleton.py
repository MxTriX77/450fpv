"""Stem skeletons for the vegetation generators (p1-v13-shrub, design decision 1; V01 grows the same module):
`count` stems fanning out of a base disc, each a leaning, arching tube that branches twice above `bare_base` into
twigs, a stem-level fork or two, and the tube meshes of both per LOD. Plain Python in asset space (lib.frame: +Y up,
metres), drawing from the caller's seeded random.Random.

The stems are a thicket's, not a bundle of stilts (p1-v13-shrub review H-2 and F-1, its item 1; p1-v01-locust task
1.5, after letter H): each leaves a random place on the base disc and leans to a tip of its own, anywhere within
TIP_SPREAD of the clump's half-width (the caller's `tip_r`) at up to TIP_AZIMUTH either side of its base's azimuth, so
the stems fan out from a narrow base and some cross, the leaning ones' tips reaching out of the leafy envelope. A stem from s to its tip T is the
curve p(t) = s + t (T - s) + arch * R * (t^2 - t) * e for t in [0, 1], R its rise T_y - s_y and e the horizontal
unit from s towards T: it leans along e and bows arch * R / 4 inside its chord at mid-height, rising steeper at the
base and leaning out towards the tip (the sheet's arch: how far stems bow outward). Its height is linear in t. Base
diameters are mixed within the caller's `stem_d` (lo, hi), lo + (hi - lo) u^2 (more young thin stems than old thick
ones, assumed), the thicker stems the taller. The thickest one or two stems fork (STEM_FORKS): a forked stem leaves
its parent at FORK_AT of its rise towards a tip of its own, its azimuth turned FORK_TURN off its parent's, taking
FORK_SHARE of its parent's base section.

Diameters follow Leonardo's rule (the sheet's group rule; the pipe model): a stem's section at any height is the sum
of the sections of everything it carries above it, its tip, the first-order twigs that leave higher up and a forked
stem that leaves higher up; a first-order twig's section is its forks' (a twig with one fork passes it on whole, one
with none ends at TIP_D_M). A stem shares its base section out: its first-order twigs as many as fit at TWIG1_D's
least (evenly spread over its nodes, the rest left off), each about the stem's even share of it within TWIG1_D, the
remainder its tip, at least TIP_D_M. RINGS + 1 samples of the curve carry the LOD0 tube, its radius at each the
section above it; the chords between every (RINGS / SEGMENTS)-th sample are the stem's straight `segments`, which
carry the LOD1 tube and which the generator turns into capsules and hooks, so mesh and collision come from the same
points. A chord's sag off the curve is arch * R / (4 SEGMENTS^2): 9 mm on a 1.9 m stem at arch 0.15, under the
audit's 50 mm (design: risks).

Twigs: a first-order twig leaves a stem at each node, NODE_SPACING_M apart in height above `bare_base`, tilted
from the stem's tangent by the fork angle (the sheet's fork_angle_deg; FORK_DEG unless the caller gives one) at
any azimuth about the stem (AZIMUTH_JITTER), and reaching FIRST_REACH of the way to the clump's envelope along its
direction; two second-order twigs fork from its end, spread by SPREAD_DEG, and reach the envelope with an outward
bias (1 - 0.3 u^2 of the way). The envelope is the caller's `reach(p, d)`: the distance from p along the unit
direction d to the clump's surface, 0 when p lies outside. A first-order twig of TWIG1_D (the sheet's 3-8 mm twigs)
keeps its diameter to its end, where its forks share its section, each 3 mm or more, and taper to TIP_D_M.

Tubes (`tubes`): stems STEM_SIDES[lod]-sided, 6 at LOD0 over the rings and 4 at LOD1 over the segment ends, the tip
closed by a cap; twigs TWIG_SIDES-sided (3) at LOD0 only; nothing from LOD2 on. The rings of a tube share a
parallel-transported frame, so it does not twist. UVs are metres along and around the member, V up the member
(AST-07: the bark recipe tiles at 1 m), each member's from its own patch of the tile (`bark_patch`; a shrub's too
since p1-v01-locust task 1.5: from the tile's corner every 1-3 cm stem mapped bark_brown_02's first 5 cm of columns,
whose linear luminance 0.146 is 37 % over the tile's 0.107, so the stems read pale).

Trees (p1-v01-locust, design decision 1; `grow` and `tree_tubes`): every member is a polyline of straight segments,
its rings at the points, so a capsule per segment is the tube's own axis. `stems` trunks from a base disc rise to
the first fork, leaning `lean_deg` (an edge tree's lean) and wandering by `sinuosity` at every joint, TRUNK_STEP_M
apart (the sheet's capsules per 1-2 m); the trunk tapers from BUTT * dbh at the ground through dbh at 1.3 m. At the
first fork it splits into `main_limbs` limbs by Leonardo's rule (the trunk's section shared out, LIMB_SHARE), which
leave at the fork angle from the trunk's axis and, like all wood, turn up UPTURN_DEG_PER_M as they grow (the locust's
ascending limbs). Every member of an order under `orders` carries `children_per_order` children on average, spread
over CHILD_SPAN of its length at golden-angle azimuths, leaving at the fork angle; a child starts at half its
parent's base diameter (the design's halving per order) with its own spread, and is SLENDERNESS times that long, so
its length halves too, but at most REACH_SHARE of its reach to the surface of the caller's crown envelope
(`inside(p)`, `reach(p, d)`, as for the shrub); a member thins linearly to half its base, the last order to
TIP_D_M, and ends where it would leave the envelope. A member thinner than
TWIG_D_M at its base is a twig (the sheet's 10 mm: tubes, shell and records; thicker wood is a capsule per segment):
a zigzag of steps of at most TWIG_STEP_M, each turned TWIG_KINK_DEG the other way, a ring at every kink.
"""
import math

RINGS = 6
SEGMENTS = 3
NODE_SPACING_M = 0.2
NODE_JITTER = (0.3, 0.7)        # a node's place within its spacing slot
TIP_SPREAD = 0.9                # a stem's tip within this share of the clump's half-width (assumed, H)
TIP_AZIMUTH = 1.2               # radians either side of its base's azimuth: the stems fan out and some cross (H, F)
STEM_FORKS = (1, 2)             # stem-level forks per clump, on its thickest stems (p1-v13-shrub review item 1)
FORK_SHARE = (0.35, 0.5)        # a forked stem's share of its parent's base section (Leonardo's rule)
FORK_AT = (0.25, 0.5)           # of its parent's rise, where it leaves
FORK_TURN = (0.6, 1.4)          # radians its tip's azimuth turns off its parent's, either way
FORK_TOP = (0.85, 1.0)          # its tip's height over its parent's
TWIG1_D = (0.0045, 0.008)       # a first-order twig's base diameter: the sheet's 3-8 mm twigs, its two forks then
                                # 3.2 mm or more (d / sqrt 2)
SHARE_JITTER = (0.7, 1.3)       # a first-order twig's section over its stem's even share
AZIMUTH_JITTER = math.pi        # radians about the outward azimuth, for a first-order twig: any side of its stem
                                # (1.0 left the crown's core empty, a hole into the clump from 20 deg above)
FORK_DEG = (30.0, 50.0)         # the sheet's fork_angle_deg default
SPREAD_DEG = (20.0, 40.0)
PITCH_JITTER = 0.25             # added to a second-order direction's Y before renormalising
FIRST_REACH = (0.45, 0.65)
MIN_TWIG_M = 0.05
STEM_SIDES = {0: 6, 1: 4}
TWIG_SIDES = 3
AXES = ((1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0))

# Trees (design decision 1). Labels: assumed unless stated.
TWIG_D_M = 0.010            # the sheet's threshold (REAL-05): thinner wood is a twig, thicker a capsule
TIP_D_M = 0.003             # the sheet's thinnest twig
DBH_AT_M = 1.3              # where dbh is measured (forestry convention, published)
BUTT = 1.1                  # the trunk's diameter at the ground over dbh: the root flare
TRUNK_STEP_M = 1.5          # trunk segments, one capsule each: the sheet's capsules per 1-2 m
BRANCH_STEP_M = 0.35        # limbs and branches: one capsule per segment
TWIG_STEP_M = 0.1           # review-physics PR6: a ring at every kink, at most 0.1 m apart
TWIG_KINK_DEG = 15.0        # design decision 1: the zigzag's kink per 0.1 m
WANDER_DEG = 20.0           # the axis's random walk at sinuosity 1, either way per joint of a metre's steps, and as
                            # the root of the step for shorter ones: the sheet's 0.4 turns a 0.35 m step up to 4.7 deg
LIMB_SHARE = (0.7, 1.3)     # a limb's weight in the split of the trunk's section at the first fork
CHILD_SPAN = (0.2, 0.95)    # where along its parent a child leaves; its tip stays free
CHILD_RATIO = 0.5           # a child's base diameter over its parent's (design decision 1: halving per order)
CHILD_SPREAD = (0.8, 1.2)   # each child's own share of that
SLENDERNESS = 70.0          # a member's length over its base diameter at most, so the length halves with it too
                            # (assumed: a 10 mm branch 0.7 m, a 40 mm one 2.8 m, a 5 mm twig 0.35 m)
REACH_SHARE = (0.6, 0.95)   # and at most this share of its reach to the crown's surface
UPTURN_DEG_PER_M = 5.0      # wood turns up toward the light as it grows: the locust's ascending limbs (F)
MIN_STEP_M = 0.02           # a last step shorter than this to the crown's surface is left off
GOLDEN = math.radians(137.5)
AZIMUTH_SPREAD = math.radians(25.0)
MIN_MEMBER_M = 0.08         # a child that would be shorter is not grown
TREE_SIDES = (12, 8, 6, 3)  # trunk, limb, branch, twig, at every LOD (p1-v01-locust task 1.3)
RING_STEP = ((1, 2, 8), (1, 2, 8), (1, 2, None))  # trunk, limb, branch: every this-th ring per LOD, None the chord:
                            # a dropped ring stands about a pixel or less off the kept polyline at the LOD's switch
                            # (derived: the 4.7 deg wander a 0.35 m joint and the 5 deg a metre upturn at 9.35 and 116.9 m)
TWIG_KEEP = (1, 1, 2)       # every this-th twig per LOD, from LOD1 on its chord (p1-v01-locust task 1.2g)
DOME_RINGS = 2              # rings between a dome's rim and its pole: facets within r (1 - cos 15 deg), 3.5 % of r
BARK_STEP = (0.7548776662466927, 0.5698402909980532)  # the R2 sequence's steps, 1/g and 1/g^2 for g the plastic
                            # number: member n's bark patch starts at frac(0.5 + n * step), spread evenly over the tile


class Stem:
    """One stem from `base` to `tip` bowing by `arch` (module docstring), `parent` the index of the stem it forks
    from (None from the ground). Once `thicken`ed: `rings` [(point, radius)] from base to tip and `segments`
    [(p0, p1, r0, r1)], the straight pieces between every (RINGS / SEGMENTS)-th ring."""

    def __init__(self, base, tip, arch, parent=None):
        self.base, self.tip, self.arch, self.parent = base, tip, arch, parent
        self.rise = tip[1] - base[1]
        across = (tip[0] - base[0], 0.0, tip[2] - base[2])
        self.out = normalize(across) if norm(across) > 1e-9 else (0.0, 0.0, 0.0)
        self.rings, self.segments, self.carried, self.tip_d = [], [], [], None

    def point(self, t):
        bow = self.arch * self.rise * (t * t - t)
        return tuple(self.base[a] + t * (self.tip[a] - self.base[a]) + bow * self.out[a] for a in range(3))

    def tangent(self, t):
        bow = self.arch * self.rise * (2 * t - 1)
        return normalize(tuple(self.tip[a] - self.base[a] + bow * self.out[a] for a in range(3)))

    def at_height(self, y):
        """The t at which the stem stands at height y (its height is linear in t)."""
        return (y - self.base[1]) / self.rise

    def lean_deg(self):
        """The chord's lean off the vertical, degrees."""
        return math.degrees(math.atan2(math.hypot(self.tip[0] - self.base[0], self.tip[2] - self.base[2]), self.rise))

    def section(self, t):
        """Its section, as a squared diameter, at t: its tip's and those of everything that leaves above t."""
        return self.tip_d ** 2 + sum(s for at, s in self.carried if at > t)

    def thicken(self, tip_d, carried):
        """Sets the diameters by Leonardo's rule: `carried` [(t, squared diameter)] of what leaves it, its tip
        `tip_d`; then its rings and segments."""
        self.tip_d, self.carried = tip_d, sorted(carried)
        self.rings = [(self.point(k / RINGS), math.sqrt(self.section(k / RINGS)) / 2) for k in range(RINGS + 1)]
        ends = self.rings[::RINGS // SEGMENTS]
        self.segments = [(a[0], b[0], a[1], b[1]) for a, b in zip(ends, ends[1:])]


class Twig:
    """A straight twig from p0 (radius r0) to p1 (radius r1); `level` 1 leaves a stem, 2 leaves a level-1 twig, whose
    index in the build's twigs is `parent`."""

    def __init__(self, p0, p1, r0, r1, level, parent=None):
        self.p0, self.p1, self.r0, self.r1, self.level, self.parent = p0, p1, r0, r1, level, parent


def build(rng, count, base_r, lengths, stem_d, arch, bare_base, reach, tip_r, fork_deg=FORK_DEG):
    """(stems, twigs) of a thicket clump (module docstring): `count` stems from a disc of radius `base_r`, each
    rising a height drawn from `lengths` (lo, hi), of a base diameter drawn from `stem_d` (lo, hi) metres, leaning to
    a tip within TIP_SPREAD of `tip_r` from the axis, bowing by `arch`; the thickest one or two forked; their
    twigs above `bare_base` grown out to `reach`, leaving their stems at an angle drawn from `fork_deg` (lo, hi)
    degrees; every diameter by Leonardo's rule. The stems from the ground come first, the forked ones after them."""
    stems, base_d = [], []
    for i in range(count):
        phi = 2 * math.pi * (i + rng.random()) / count
        rho = base_r * math.sqrt(rng.random())
        u = rng.random()
        base_d.append(stem_d[0] + (stem_d[1] - stem_d[0]) * u * u)
        y = lengths[0] + (lengths[1] - lengths[0]) * (u + rng.random()) / 2
        psi = phi + rng.uniform(-TIP_AZIMUTH, TIP_AZIMUTH)
        stems.append(Stem((rho * math.cos(phi), 0.0, rho * math.sin(phi)), _tip(rng, psi, y, tip_r), arch))
    budget = [d * d for d in base_d]  # each stem's base section, shared out below
    thickest = sorted(range(count), key=lambda i: -base_d[i])
    for i in [i for i in thickest if budget[i] * FORK_SHARE[0] >= TWIG_D_M ** 2][:rng.randint(*STEM_FORKS)]:
        parent = stems[i]
        share = rng.uniform(*FORK_SHARE)
        t = rng.uniform(*FORK_AT)
        psi = math.atan2(parent.tip[2], parent.tip[0]) + rng.choice((-1, 1)) * rng.uniform(*FORK_TURN)
        fork = Stem(parent.point(t), _tip(rng, psi, parent.tip[1] * rng.uniform(*FORK_TOP), tip_r), arch, i)
        parent.carried.append((t, share * budget[i]))
        budget.append(share * budget[i])
        budget[i] -= share * budget[i]
        stems.append(fork)
    twigs = []
    for stem, section in zip(stems, budget):
        groups = _twigs(rng, stem, bare_base, reach, fork_deg)
        twigs += _share(rng, stem, section, groups, len(twigs))
    return stems, twigs


def _tip(rng, azimuth, y, tip_r):
    """A stem's tip at height y, at the azimuth, within TIP_SPREAD of `tip_r` from the axis (uniform over the disc)."""
    rho = TIP_SPREAD * tip_r * math.sqrt(rng.random())
    return (rho * math.cos(azimuth), y, rho * math.sin(azimuth))


def _share(rng, stem, section, groups, first):
    """Leonardo's rule on one stem: its own base `section` (squared diameter; a forked stem's already counted in
    `carried`) shared out over the first-order twigs of `groups` [(t, twig1, [twig2])] that fit at TWIG1_D's least,
    evenly spread, and its tip. Thickens the stem and returns the kept twigs, each first-order one followed by its
    forks, `parent` the index of a fork's twig counting from `first`."""
    lo, hi = TWIG1_D[0] ** 2, TWIG1_D[1] ** 2
    spare = section - TIP_D_M ** 2
    keep = min(len(groups), max(0, int(spare / lo)))
    kept = [groups[int((k + 0.5) * len(groups) / keep)] for k in range(keep)]
    want = [min(hi, max(lo, spare / keep * rng.uniform(*SHARE_JITTER))) for _ in kept] if keep else []
    if sum(want) > spare:  # held to the stem's section: the excess over the least shrinks evenly
        k = (spare - lo * keep) / (sum(want) - lo * keep)
        want = [lo + (w - lo) * k for w in want]
    twigs = []
    for (t, twig, forks), s in zip(kept, want):
        d = math.sqrt(s)
        twig.r0 = d / 2
        twig.r1 = d / 2 if forks else TIP_D_M / 2
        index = first + len(twigs)
        twigs.append(twig)
        for fork in forks:
            fork.r0, fork.r1, fork.parent = d / math.sqrt(len(forks)) / 2, TIP_D_M / 2, index
            twigs.append(fork)
    stem.thicken(math.sqrt(section - sum(want)), stem.carried + [(t, s) for (t, _, _), s in zip(kept, want)])
    return twigs


def _twigs(rng, stem, bare_base, reach, fork_deg):
    """[(t, first-order twig, [its forks])] at the stem's nodes, NODE_SPACING_M apart in height from `bare_base` or
    its base, whichever is higher; their diameters are set by `_share`."""
    groups = []
    y0 = max(bare_base, stem.base[1])
    for k in range(int((stem.tip[1] - y0) / NODE_SPACING_M)):
        t = stem.at_height(y0 + (k + rng.uniform(*NODE_JITTER)) * NODE_SPACING_M)
        node, tangent = stem.point(t), stem.tangent(t)
        azimuth = math.atan2(node[2], node[0]) if math.hypot(node[0], node[2]) > 0.05 else rng.uniform(0, 2 * math.pi)
        azimuth += rng.uniform(-AZIMUTH_JITTER, AZIMUTH_JITTER)
        fork = math.radians(rng.uniform(*fork_deg))
        d = normalize(add(scale(tangent, math.cos(fork)), (math.cos(azimuth) * math.sin(fork), 0.0, math.sin(azimuth) * math.sin(fork))))
        distance = reach(node, d)
        if distance < MIN_TWIG_M:
            continue
        end = add(node, scale(d, rng.uniform(*FIRST_REACH) * distance))
        forks = []
        for side in (-1, 1):
            d2 = _rotate_y(d, side * math.radians(rng.uniform(*SPREAD_DEG)))
            d2 = normalize((d2[0], d2[1] + rng.uniform(-PITCH_JITTER, PITCH_JITTER), d2[2]))
            distance = reach(end, d2)
            u = rng.random()
            if distance < MIN_TWIG_M:
                continue
            forks.append(Twig(end, add(end, scale(d2, (1 - 0.3 * u * u) * distance)), 0.0, 0.0, 2))
        groups.append((t, Twig(node, end, 0.0, 0.0, 1), forks))
    return groups


class Member:
    """One member of a tree: `points` from its base to its tip with the straight segments between them, `radii` at
    the points; its `order` (0 a trunk, 1 a main limb, then branches), `parent` and `limb` (indices into the tree's
    members: no parent and no limb on a trunk, a limb its own limb), `attach` the arc length along the parent where it
    leaves, and its `children` (indices). It is a `twig` when its base is thinner than TWIG_D_M."""

    def __init__(self, points, radii, order, parent=None, limb=None, attach=0.0):
        self.points, self.radii, self.order = points, radii, order
        self.parent, self.limb, self.attach = parent, limb, attach
        self.children = []

    @property
    def twig(self):
        return 2 * self.radii[0] < TWIG_D_M

    def lengths(self):
        return [norm(sub(b, a)) for a, b in zip(self.points, self.points[1:])]

    def length(self):
        return sum(self.lengths())

    def at(self, s):
        """(point, unit tangent, radius) at arc length s from the base, held to the member."""
        lengths = self.lengths()
        for k, l in enumerate(lengths):
            if s <= l or k == len(lengths) - 1:
                t = min(1.0, max(0.0, s / l))
                a, b = self.points[k], self.points[k + 1]
                return add(a, scale(sub(b, a), t)), normalize(sub(b, a)), self.radii[k] + (self.radii[k + 1] - self.radii[k]) * t
            s -= l

    def segments(self):
        """[(p0, p1, r0, r1)] of its straight segments."""
        return [(a, b, ra, rb) for a, b, ra, rb in zip(self.points, self.points[1:], self.radii, self.radii[1:])]


def trunks(rng, stems, base_r, dbh, first_fork, sinuosity, lean_deg):
    """The trunks of a tree (module docstring): `stems` from a base disc of radius `base_r` (one at the centre),
    each leaning `lean_deg` outward (a lone trunk at a random azimuth), up to the first fork's height exactly."""
    members = []
    lean = math.radians(lean_deg)
    phase = rng.uniform(0.0, 2 * math.pi)
    for i in range(stems):
        azimuth = phase + 2 * math.pi * i / stems + (rng.uniform(-0.3, 0.3) if stems > 1 else 0.0)
        base = (0.0, 0.0, 0.0) if stems == 1 else (base_r * math.cos(azimuth), 0.0, base_r * math.sin(azimuth))
        direction = (math.sin(lean) * math.cos(azimuth), math.cos(lean), math.sin(lean) * math.sin(azimuth))
        steps = max(1, math.ceil(first_fork / math.cos(lean) / TRUNK_STEP_M))
        points = _wander(rng, base, direction, first_fork / math.cos(lean), steps, sinuosity)
        rise = points[-1][1] - base[1]  # held to the first fork's height exactly
        points = [(base[0] + (p[0] - base[0]) * first_fork / rise, p[1] * first_fork / rise,
                   base[2] + (p[2] - base[2]) * first_fork / rise) for p in points]
        radii = [dbh / 2 * (BUTT + (1 - BUTT) * p[1] / DBH_AT_M) for p in points]
        members.append(Member(points, radii, 0))
    return members


def branches(rng, members, main_limbs, orders, children_per_order, fork_deg, sinuosity, limb_length, crown):
    """Grows the main limbs on the `members`' trunks and every order after them (module docstring), appending to
    `members`: a limb `limb_length` (lo, hi) metres long, every member ending where it would leave the `crown`
    (inside(p), reach(p, d))."""
    stems = len(members)
    for trunk_index in range(stems):
        trunk = members[trunk_index]
        fork, tangent, r_fork = trunk.points[-1], normalize(sub(trunk.points[-1], trunk.points[-2])), trunk.radii[-1]
        weights = [rng.uniform(*LIMB_SHARE) for _ in range(main_limbs)]
        spin = rng.uniform(0.0, 2 * math.pi)
        for k, w in enumerate(weights):
            d = _off(tangent, math.radians(rng.uniform(*fork_deg)), spin + 2 * math.pi * k / main_limbs + rng.uniform(-0.3, 0.3))
            r = r_fork * math.sqrt(w / sum(weights))
            index = len(members)
            members.append(_member(rng, fork, d, rng.uniform(*limb_length), r, 1, trunk_index, index, trunk.length(),
                                   sinuosity, crown))
            trunk.children.append(index)
    queue = [i for i, m in enumerate(members) if m.order == 1]
    while queue:
        parent_index = queue.pop(0)
        parent = members[parent_index]
        if parent.order >= orders:
            continue
        count = int(children_per_order) + (rng.random() < children_per_order - int(children_per_order))
        length = parent.length()
        phase = rng.uniform(0.0, 2 * math.pi)
        for k in range(count):
            s = length * (CHILD_SPAN[0] + (CHILD_SPAN[1] - CHILD_SPAN[0]) * (k + rng.uniform(0.2, 0.8)) / count)
            p, t, r_local = parent.at(s)
            angle = math.radians(rng.uniform(*fork_deg))
            d = _off(t, angle, phase + k * GOLDEN + rng.uniform(-AZIMUTH_SPREAD, AZIMUTH_SPREAD))
            r = max(TIP_D_M / 2, min(r_local, parent.radii[0] * CHILD_RATIO * rng.uniform(*CHILD_SPREAD)))
            child_length = min(SLENDERNESS * 2 * r, rng.uniform(*REACH_SHARE) * crown.reach(p, d))
            if child_length < MIN_MEMBER_M:
                continue
            index = len(members)
            last = parent.order + 1 >= orders
            members.append(_member(rng, p, d, child_length, r, parent.order + 1, parent_index, parent.limb, s, sinuosity,
                                   crown, last))
            parent.children.append(index)
            queue.append(index)
    return members


def _member(rng, start, direction, length, r, order, parent, limb, attach, sinuosity, crown, last=False):
    """A limb or branch, or a twig when thinner than TWIG_D_M: its polyline, ending where it would leave the crown,
    and radii thinning linearly to half its base, or the last order to TIP_D_M."""
    twig = 2 * r < TWIG_D_M
    if twig:
        points = _zigzag(rng, start, direction, length, sinuosity, crown)
    else:
        points = _wander(rng, start, direction, length, max(1, math.ceil(length / BRANCH_STEP_M)), sinuosity, crown,
                         math.radians(UPTURN_DEG_PER_M))
    tip = TIP_D_M / 2 if last else max(TIP_D_M / 2, r / 2)
    arcs = [0.0]
    for a, b in zip(points, points[1:]):
        arcs.append(arcs[-1] + norm(sub(b, a)))
    radii = [r + (min(tip, r) - r) * s / arcs[-1] for s in arcs]
    return Member(points, radii, order, parent, limb, attach)


def _wander(rng, start, direction, length, steps, sinuosity, crown=None, upturn=0.0):
    """`steps` equal straight steps over `length`, the heading turned at every joint by up to sinuosity * WANDER_DEG
    times the root of the step either way about a random axis across it, and by `upturn` radians a metre toward the
    vertical; within a `crown`, it ends where it would leave it."""
    points, d, step = [start], direction, length / steps
    turn = math.radians(sinuosity * WANDER_DEG) * math.sqrt(step)
    for k in range(steps):
        point, out = _step(points[-1], d, step, crown)
        if point is None:
            break
        points.append(point)
        if out:
            break
        if k < steps - 1:
            d = _up(_off(d, rng.uniform(-turn, turn), rng.uniform(0.0, 2 * math.pi)), upturn * step)
    if len(points) < 2:
        raise ValueError(f"a member leaves the crown at its start {start}")
    return points


def _zigzag(rng, start, direction, length, sinuosity, crown=None):
    """A twig's zigzag: steps of at most TWIG_STEP_M, the heading swinging TWIG_KINK_DEG the other way at every kink
    in one plane about the mean heading (starting half a kink off it), and wandering as a branch does; within a
    `crown`, it ends where it would leave it."""
    steps = max(1, math.ceil(length / TWIG_STEP_M - 1e-9))
    step, half = length / steps, math.radians(TWIG_KINK_DEG) / 2
    side = 1 if rng.random() < 0.5 else -1
    across = _off(direction, math.pi / 2, rng.uniform(0.0, 2 * math.pi))  # the zigzag's plane: heading and this
    turn = math.radians(sinuosity * WANDER_DEG) * math.sqrt(step)  # the wander, as along a branch
    heading, points = direction, [start]
    for _ in range(steps):
        point, out = _step(points[-1], normalize(add(scale(heading, math.cos(half)), scale(across, side * math.sin(half)))),
                           step, crown)
        if point is None:
            break
        points.append(point)
        if out:
            break
        side = -side
        heading = _off(heading, rng.uniform(-turn, turn), rng.uniform(0.0, 2 * math.pi))
        across = normalize(sub(across, scale(heading, dot(across, heading))))
    if len(points) < 2:
        raise ValueError(f"a twig leaves the crown at its start {start}")
    return points


def _step(point, d, step, crown):
    """(next point, whether it is the crown's surface): `step` along d, or as far as the crown's surface when that
    comes first; None for a stub under MIN_STEP_M."""
    nxt = add(point, scale(d, step))
    if crown is None or crown.inside(nxt):
        return nxt, False
    reach = crown.reach(point, d)
    return (add(point, scale(d, reach)) if reach >= MIN_STEP_M else None), True


def _up(d, angle):
    """The unit d turned `angle` radians toward the vertical, never past it."""
    across = (-d[0] * d[1], 1.0 - d[1] * d[1], -d[2] * d[1])  # the vertical's part across d
    length = norm(across)
    if angle <= 0 or length < 1e-9:
        return d
    a = min(angle, math.acos(max(-1.0, min(1.0, d[1]))))
    return normalize(add(scale(d, math.cos(a)), scale(across, math.sin(a) / length)))


def _off(along, angle, azimuth):
    """The unit direction `angle` radians off the unit `along`, at `azimuth` about it in a frame fixed by `along`."""
    e1 = normalize(cross(along, _helper(along)))
    e2 = cross(along, e1)
    around = add(scale(e1, math.cos(azimuth)), scale(e2, math.sin(azimuth)))
    return normalize(add(scale(along, math.cos(angle)), scale(around, math.sin(angle))))


def tree_tubes(mesh, members, lod, slot_of):
    """Adds the tree's tubes for `lod` to a lib.geometry.Mesh, each in its kind's TREE_SIDES at every LOD: a member's
    rings at its points, from LOD1 on every RING_STEP-th of them and its last, a branch at LOD2 its chord; at LOD0 a
    capsule member closed at both ends by domes of its end radii, its capsules' own caps (the base's buried in its
    parent, or a trunk's in the ground; a trunk's top the crotch the limbs leave), at LOD1 the trunk and the limbs at
    their free end only, from LOD2 on nothing; a twig open at both ends. `slot_of(member)` names the slot. Every LOD
    holds LOD0's wood, in its own colour (p1-v01-locust task 1.2g: the leaf cards that stood for the wood a far LOD
    dropped were 1.5-1.8 times as bright per pixel as the wood, so V01's LOD2 came out 10-14 % over LOD0): from LOD1 on
    a tube's radii grow by its length over its kept rings' polyline, and a twig is one band along its chord, its radii
    grown by its length over its chord, every TWIG_KEEP[lod]-th twig standing for the rest, its radii grown again by
    the twigs' summed length times mean radius over the kept ones'. A far LOD keeps LOD0's sides and drops rings
    instead (task 1.3): with fewer sides, shaded smooth, a 3-sided tube shows each face's normals over 120 degrees, so
    more of its visible surface faces the camera at a grazing angle, where URP's Fresnel term Pow4(1 - N.V) is whole
    (its projected-area mean over the tube's roll 0.192, against 0.072 for 6 sides, 0.057 for 8 and 0.045 for 12;
    derived), and V01_M's LOD2 wood took 70 % more specular than LOD0's at 50 and 190 m (measured); given LOD0's
    spread of normals instead, the 3- and 4-sided trunk and limbs, few and large, took the review's sun by how their
    faces happened to turn (V01_M's LOD2 wood 16 % dark by day, measured): a tube of few sides cannot shade as a round
    one from every side. A far tube starts its frame, and so its U, where LOD0's starts: from its own first chord the
    world axis that seeds the frame can change, a quarter turn, and V01_M's trunk then showed other texels of its
    bark (the far LODs' wood 7-8 % dark unlit, measured). Each member's UVs start at its own patch of the bark tile (`bark_patch`), the same at every
    LOD (p1-v01-locust task 1.2l): from one corner, a twig of 1-3 cm round mapped only the tile's first columns, which
    in bark_brown_01 read 40 % darker than the tile (linear luminance 0.096 against 0.163), and its far mips the whole
    tile, so every mesh LOD's wood brightened with distance (V01_M's LOD0 wood albedo 23 % from 9.4 to 190 m, unlit,
    measured) while the impostor's bake kept the corner."""
    twigs = [m for m in members if m.twig]
    kept = twigs[::TWIG_KEEP[lod]]
    share = sum(_twig_size(m) for m in twigs) / sum(_twig_size(m) for m in kept) if kept else 1.0
    kept = {id(m) for m in kept}
    for index, member in enumerate(members):
        kind = 3 if member.twig else min(member.order, 2)
        sides, rings = TREE_SIDES[kind], list(zip(member.points, member.radii))
        if lod:
            if member.twig:
                if id(member) not in kept:
                    continue
                kept_rings, grow = [rings[0], rings[-1]], share
            else:
                step = RING_STEP[kind][lod] or len(rings) - 1
                kept_rings, grow = rings[::step] + ([rings[-1]] if (len(rings) - 1) % step else []), 1.0
            grow *= member.length() / sum(norm(sub(q[0], p[0])) for p, q in zip(kept_rings, kept_rings[1:]))
            rings = [(p, r * grow) for p, r in kept_rings]
        start = normalize(sub(member.points[1], member.points[0]))  # LOD0's first tangent, whose frame a far LOD takes
        frame = _tube(mesh, rings, sides, slot_of(member), cap=False, uv0=bark_patch(index),
                      first=cross(start, _helper(start)) if lod else None)
        if not member.twig and (not lod or (lod == 1 and kind < 2)):
            if not lod:
                _dome(mesh, rings[0], scale(normalize(sub(member.points[1], member.points[0])), -1.0), sides,
                      slot_of(member), frame[0])
            _dome(mesh, rings[-1], normalize(sub(rings[-1][0], rings[-2][0])), sides, slot_of(member), frame[1])


def bark_patch(index):
    """Where member `index`'s UVs start in the bark tile, (u, v) in [0, 1): the R2 sequence (BARK_STEP), so a tree's
    members between them sample the whole tile, as its far mips do."""
    return tuple((0.5 + (index + 1) * step) % 1.0 for step in BARK_STEP)


def _twig_size(member):
    """A twig's length times the sum of its end radii (twice its mean): what its silhouette goes with."""
    return member.length() * (member.radii[0] + member.radii[-1])


def _dome(mesh, ring, outward, sides, slot, u):
    """A hemisphere of the ring's radius beyond its centre along the unit `outward`, DOME_RINGS rings and a pole,
    its seam at the tube's frame vector `u`."""
    centre, r = ring
    v = cross(outward, u)
    rows = []
    for k in range(DOME_RINGS + 1):
        a = math.pi / 2 * k / (DOME_RINGS + 1)
        c, h = r * math.cos(a), r * math.sin(a)
        rows.append([mesh.vertex(add(centre, add(scale(outward, h), add(scale(u, c * math.cos(t)), scale(v, c * math.sin(t))))))
                     for t in (2 * math.pi * j / sides for j in range(sides))])
    pole = mesh.vertex(add(centre, scale(outward, r)))
    for lo, hi in zip(rows, rows[1:]):
        for j in range(sides):
            k = (j + 1) % sides
            quad = [lo[j], lo[k], hi[k], hi[j]]
            mesh.face(quad, normalize(sub(_mean([mesh.vertices[q] for q in quad]), centre)), slot)
    for j in range(sides):
        tri = [rows[-1][j], rows[-1][(j + 1) % sides], pole]
        mesh.face(tri, normalize(sub(_mean([mesh.vertices[q] for q in tri]), centre)), slot)


def _mean(points):
    return scale(tuple(sum(p[a] for p in points) for a in range(3)), 1.0 / len(points))


def tubes(mesh, stems, twigs, lod, stem_slot, twig_slot):
    """Adds the skeleton's tubes for `lod` to a lib.geometry.Mesh: stems STEM_SIDES[lod]-sided over their rings
    (LOD0) or their segment ends (LOD1), capped at the tip; twigs TWIG_SIDES-sided at LOD0. Nothing from LOD2 on.
    Each stem, then each twig, takes its own patch of the bark tile (`bark_patch`), the same at every LOD."""
    sides = STEM_SIDES.get(lod)
    if sides is None:
        return
    step = 1 if lod == 0 else RINGS // SEGMENTS
    for k, stem in enumerate(stems):
        _tube(mesh, stem.rings[::step], sides, stem_slot, cap=True,
              tangents=[stem.tangent(j / RINGS) for j in range(0, RINGS + 1, step)], uv0=bark_patch(k))
    if lod == 0:
        for k, twig in enumerate(twigs, len(stems)):
            _tube(mesh, [(twig.p0, twig.r0), (twig.p1, twig.r1)], TWIG_SIDES, twig_slot, cap=False, uv0=bark_patch(k))


def _tube(mesh, rings, sides, slot, cap, tangents=None, uv0=(0.0, 0.0), first=None):
    """A tube over [(point, radius)] rings, each ring perpendicular to its tangent (given, or the chord through its
    neighbours): one quad per side and band with its outward normal and metre UVs (U around, V along) from `uv0`,
    and a polygon cap over the last ring when `cap`. Its frame starts at `first` made square to the first tangent, or
    from the world axis least along it. Returns the frame's first and last vector across the tube, where its rings'
    first vertices lie."""
    u0, v0 = uv0
    points = [p for p, _ in rings]
    last = len(points) - 1
    if tangents is None:
        tangents = [normalize(sub(points[min(i + 1, last)], points[max(i - 1, 0)])) for i in range(len(points))]
    if first is None:
        u = normalize(cross(tangents[0], _helper(tangents[0])))
    else:
        u = normalize(sub(first, scale(tangents[0], dot(first, tangents[0]))))
    first = u
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
            mesh.face(quad, normal, slot, [(u0 + j * c0, v0 + along[i]), (u0 + (j + 1) * c0, v0 + along[i]),
                                           (u0 + (j + 1) * c1, v0 + along[i + 1]), (u0 + j * c1, v0 + along[i + 1])])
    if cap:
        mesh.face(ring_ids[last], tangents[last], slot)
    return first, u


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
