"""tree: the shared tree generator of the vegetation sheet, the black locust V01 first (p1-v01-locust, design
decisions 1, 2, 4 and 6; review-physics PR1-PR8; AST-05, AST-08). The origin is the base centre on the ground.

The crown is an envelope (Crown) the branches grow out to: an ellipsoid of revolution about the vertical through the
trunks' tops, `crown_width_m` wide, from CROWN_BELOW of the crown's height under the first fork up to `height_m`,
widest at WAIST of that span, shrunk by CARD_MARGIN of a leaf so the leaves at the twigs' tips keep the width. The
skeleton is lib.skeleton's `trunks` and `branches`: `stems` trunks (multi-stem clumps splay STEM_SPLAY_DEG more) to
`first_fork_m`, `main_limbs` limbs at the fork angle, `orders` orders of `children_per_order` children each, the
diameters from `dbh_m` by Leonardo's rule at the first fork and halving per order, wood under 10 mm a zigzag twig
ringed at every kink at most 0.1 m apart. Pinnate leaf cards (lib.cards.along, `leaf_m` long, every LEAF_STEP_M from
LEAF_START of the member) grow along the members of the last two orders, on the share `leaf_cover` of them (the
sheet: the share of twig tips that carry leaves); pod strips (lib.cards.hanging) hang from the share `fruit_share`
of the last order's tips. `autumn_share` of the leaves, each drawn from a stream of its own seed so the tree is the
same at any share, take the leaf_autumn slot: one crown mixing green and yellow leaves (p1-v01-locust task 1.5,
letter H), each slot its own recipe.

LODs (design decision 1): LOD0 the trunk 12-sided, limbs 8, branches 6, twigs 3, every leaf and pod; LOD1 the same
sides over every other ring, each twig one band along its chord, every other leaf of each leaf slot and pod strip;
LOD2 the trunk and limbs over every eighth ring, each branch its chord, every other twig's chord standing for two,
every fourth leaf of each leaf slot
(task 1.3: with fewer sides the far LODs' wood took 70 % more specular than LOD0's, lib.skeleton.tree_tubes). The
kept leaves take the cells of the leaves' mean colour and grow to hold every leaf's silhouette (lib.cards.thinned;
task 1.2b: LOD2 had half LOD0's crown pixels and was 13-20 % dark by day), and the wood holds LOD0's wood in its own
colour (lib.skeleton.tree_tubes, task 1.2g). Every tube is shaded smooth, so a tube shades as the round wood it
stands for (task 1.2g: flat, the 4-sided limbs of LOD1 turned a face to the review's camera that its side sun missed,
a fifth of the wood's light by day).
lod.* come from the pixel rule (935 px/rad): LOD1 where the 10 mm twigs drop under 1 px (9.35 m), LOD2 where a leaf
drops under 2 px (leaf_m * 467.5), the impostor (`impostor`, lib.impostor, built by build.py) where the crown's width
drops under 24 px (crown_width_m * 935 / 24, rounded down to 10 m; design decision 3), cull where the tree's height
drops under 2 px (height_m * 467.5), at most 2 500 m. V01_S's 24 px fall at its LOD2 switch, 116.9 m (3 m over 24 px is
0.25 m over 2), which would leave LOD2 no range, so V01_S keeps its LOD2 to the cull (task 1.3).

Metadata (design decision 4, review-physics PR4-PR8):
- every segment of wood of 10 mm or more a `bark` capsule on its chord at its mean radius, the count reported;
- the crown's `twig` shell fitted per main limb by lib.shell's cap by steps over the limb's twig, leaf and pod faces,
  a limb that 64 pieces do not pass split into its child subtrees; the audit holds the tree's union;
- snag records by the sheet's rule, each in its member's material: a `bark` segment under 30 mm an `edge`, the top
  segment of every member (all end free) a `hook`, and every twig one `hook` polyline through its rings in `twig`;
- gaps: the opening between every two members of 30 mm or more that leave one fork (the trunks of a clump from the
  ground, the limbs at the first fork, a branch and its parent), in the plane of their axes, bounded by their
  surfaces and closed by the chord at the first child fork of either, shrunk until no capsule, shell piece or twig
  record meets it, written when its largest inscribed circle is 0.5 m or more;
- wind: one crown box over the LOD0 twig, leaf and pod vertices from the lowest of them to the top, at the state's
  porosity (POROSITY, the sheet's points by leaf_cover), and per trunk a solid column of width dbh along its chord
  from the ground to that box's base; nothing landable.

Slots: bark (bark_locust, physics bark), twig (bark_locust, physics twig: the twigs are the shell's), leaf
(leaf_locust, physics twig), leaf_autumn (leaf_locust_autumn, physics twig; design decision 7, task 1.5) and pod
(pod_locust, physics twig), each only when LOD0 has faces of it.
"""
import math
import random
import time

from lib import audit, cards, meta, shell, skeleton
from lib.geometry import Mesh

BARK, TWIG, LEAF, LEAF_AUTUMN, POD = "bark", "twig", "leaf", "leaf_autumn", "pod"
PHYSICS = {BARK: "bark", TWIG: "twig", LEAF: "twig", LEAF_AUTUMN: "twig", POD: "twig"}  # per slot; a snag record takes its member's
CROWN_SHAPES = ("scraggly",)
# §V01's rows, height to crown width (the sheet's S / M / L): check holds a variant's crown to its row.
SHEET_ROWS = (((4.0, 7.0), (2.0, 4.0)), ((8.0, 12.0), (3.0, 6.0)), ((12.0, 16.0), (5.0, 8.0)))
CROWN_BELOW = 0.15          # of the crown's height over the fork: the lowest branches arch below it (assumed, F)
WAIST = 0.55                # of the envelope's span: an open crown widest above its middle (assumed, F and H)
CARD_MARGIN = 0.6           # of leaf_m, the envelope's shrink, so the leaves keep the crown's width
LIMB_LENGTH = (0.8, 1.0)    # of the crown's height over the fork: the main limbs run up near the top (assumed, F, H)
BASE_R_M = 0.15             # a clump's stems on a disc this wide (assumed: root suckers round one collar)
STEM_SPLAY_DEG = 5.0        # a clump's stems lean out this much more than a lone trunk (assumed, F)
LEAF_STEP_M = 0.05          # a leaf every this along a leafy member (assumed: the shoot's internodes)
LEAF_START = 0.2            # of a member's length, bare at its base (assumed)
LEAF_ANGLE_DEG = (45.0, 70.0)   # a leaf's angle off its shoot (assumed)
LEAF_KEEP = (1, 2, 4)       # every this-th leaf per LOD (lib.cards.thinned)
POD_M, POD_ASPECT = 0.08, 0.5   # a pod strip's length (the sheet's 7 cm pods on their stalk) and the painted map's aspect
SNAG_EDGE_M = 0.030         # the sheet: every segment under 30 mm is an edge
GAP_MEMBER_M = 0.030        # the sheet's gap rule: two members of 30 mm or more leaving one fork
GAP_MIN_M = 0.5             # the sheet: only openings of 0.5 m or more are written
ANY_YAW_M, NOSE_FIRST_M = 0.706, 0.574   # the drone's tip-to-tip and prop-line widths (knowledge §5.4, derived)
GAP_MARGIN_M = 0.02         # a polygon's sides stand this off its members' surfaces
GAP_SAMPLE_M = 0.02         # the clear-space samples' grid in the polygon's plane
GAP_GUARD_M = GAP_SAMPLE_M / math.sqrt(2) + 0.0005  # a sample this near a shape is blocked: then no shape reaches any
                            # point between samples, and no point 1 cm inside the polygon is in a shape
GAP_STEPS = 14              # bisections of the closing chord's place
POROSITY = ((0.0, 0.9), (0.6, 0.75), (0.9, 0.55))  # leaf_cover to crown porosity: the sheet's bare, part-bare, summer
LOD_RULE = "pixel rule, 935 px/rad"
# Params the sheet gives the generator that nothing can draw yet: a non-default value is refused, not ignored.
UNBUILT = (("spines", False, "no thorn cell is pinned; thorns on suckers are V16's"),)
# The object sheet's shared tree table and §V01's preset; defaults are V01_M.
PARAMS = {
    "seed": {"type": "int", "range": (0, 2**31 - 1), "default": 11, "effect": "all draws: lean, forks, limbs, leaves, pods"},
    "height_m": {"type": "float", "range": (3.0, 25.0), "default": 10.0, "effect": "total height: the crown's top; the cull follows it"},
    "dbh_m": {"type": "float", "range": (0.05, 0.8), "default": 0.16,
              "effect": "trunk diameter at 1.3 m; every branch diameter derives from it (Leonardo's rule, halving per order)"},
    "stems": {"type": "int", "range": (1, 5), "default": 1, "effect": "trunks from one base (locust suckers); each of dbh_m"},
    "first_fork_m": {"type": "float", "range": (0.3, 6.0), "default": 3.0, "effect": "height of the first fork, where the main limbs leave"},
    "main_limbs": {"type": "int", "range": (2, 6), "default": 4, "effect": "limbs at the first fork, a shell fit each"},
    "orders": {"type": "int", "range": (3, 6), "default": 5, "effect": "branching orders, the limbs the first; the last two carry the leaves"},
    "children_per_order": {"type": "float", "range": (1.5, 3.5), "default": 2.5,
                           "effect": "mean children per member; sets the twig count and the crown's density"},
    "fork_angle_deg.min": {"type": "float", "range": (15.0, 70.0), "default": 30.0, "effect": "least angle at which a child leaves its parent"},
    "fork_angle_deg.max": {"type": "float", "range": (15.0, 70.0), "default": 40.0, "effect": "greatest such angle (locust narrow)"},
    "trunk_sinuosity": {"type": "float", "range": (0.0, 1.0), "default": 0.6,
                        "effect": "random walk of the trunk's, limbs' and twigs' axes (0 straight, 1 up to 20 deg per metre's joint)"},
    "crown_shape": {"type": "str", "range": "scraggly", "default": "scraggly", "effect": "the envelope the branches fill"},
    "crown_width_m": {"type": "float", "range": (2.0, 15.0), "default": 6.0, "effect": "envelope width at the widest"},
    "lean_deg": {"type": "float", "range": (0.0, 15.0), "default": 3.0, "effect": "lean of the trunk (edge trees 8)"},
    "leaf_cover": {"type": "float", "range": (0.0, 1.0), "default": 0.6,
                   "effect": "share of the last two orders' members that carry leaves; summer 0.9, part-bare 0.6, autumn 0.3"},
    "autumn_share": {"type": "float", "range": (0.0, 1.0), "default": 0.0,
                     "effect": "share of the leaves in the leaf_autumn slot (its own recipe), drawn per leaf"},
    "leaf_m": {"type": "float", "range": (0.15, 0.36), "default": 0.25,
               "effect": "a pinnate leaf card's length from its petiole (the sheet's 15-36 cm); its far corner, 1.044 times "
                         "it, is rule (c)'s card reach"},
    "spines": {"type": "bool", "range": "true or false", "default": False, "effect": "thorn cards on suckers and low branches (not built)"},
    "fruit_share": {"type": "float", "range": (0.0, 0.5), "default": 0.0, "effect": "share of the last order's tips carrying a pod strip"},
    "impostor": {"type": "bool", "range": "true or false", "default": True,
                 "effect": "the baked far LOD, a camera-facing card from lod.impostor_switch_m (lib.impostor)"},
    "lod.lod1_switch_m": {"type": "float", "range": (0.1, 100.0), "default": 9.35,
                          "effect": "distance from which LOD1 shows: the 10 mm twigs under 1 px"},
    "lod.lod2_switch_m": {"type": "float", "range": (1.0, 500.0), "default": 116.9,
                          "effect": "distance from which LOD2 shows: a leaf under 2 px, leaf_m * 467.5"},
    "lod.impostor_switch_m": {"type": "float", "range": (1.0, 2500.0), "default": 230.0,
                              "effect": "distance from which the impostor shows, when asked for: the crown's width under 24 px, "
                                        "crown_width_m * 935 / 24 rounded down to 10 m (design decision 3)"},
    "lod.cull_m": {"type": "float", "range": (1.0, 2500.0), "default": 2500.0,
                   "effect": "distance beyond which the tree is culled: its height under 2 px, height_m * 467.5, at most 2 500"},
}


class Crown:
    """The crown's envelope (module docstring) about the vertical through `axis` (x, z)."""

    def __init__(self, axis, y0, y1, width, margin):
        self.axis = axis
        self.rw = max(width / 2 - margin, 0.2)
        self.yc = y0 + WAIST * (y1 - y0)
        self.lo, self.hi = self.yc - y0, max(y1 - margin - self.yc, 0.2)

    def inside(self, p):
        a = self.lo if p[1] < self.yc else self.hi
        return (math.hypot(p[0] - self.axis[0], p[2] - self.axis[1]) / self.rw) ** 2 + ((p[1] - self.yc) / a) ** 2 <= 1

    def reach(self, p, d):
        """Distance from p along the unit direction d to the surface, 0 when p lies outside (bisection, 0.1 mm)."""
        if not self.inside(p):
            return 0.0
        lo, hi = 0.0, 2 * max(self.rw, self.lo, self.hi)
        while hi - lo > 1e-4:
            mid = (lo + hi) / 2
            if self.inside(skeleton.add(p, skeleton.scale(d, mid))):
                lo = mid
            else:
                hi = mid
        return lo


def row(height):
    """The sheet's crown-width row for a height, or None between the rows."""
    return next((width for (h0, h1), width in SHEET_ROWS if h0 <= height <= h1), None)


def porosity(leaf_cover):
    """The crown's porosity: piecewise linear through POROSITY, held at its last point beyond it."""
    for (c0, p0), (c1, p1) in zip(POROSITY, POROSITY[1:]):
        if leaf_cover <= c1:
            return p0 + (p1 - p0) * (leaf_cover - c0) / (c1 - c0)
    return POROSITY[-1][1]


def check(params):
    """Cross-parameter rules once every key is in range: the fork angles are ordered, the first fork stands under the
    top, the crown is a shape the generator builds and as wide as its row of the sheet, and nothing asks for a look
    that is not built."""
    problems = []
    fork = params["fork_angle_deg"]
    if fork["min"] > fork["max"]:
        problems.append(f"'fork_angle_deg.min' = {fork['min']} must not exceed 'fork_angle_deg.max' = {fork['max']}")
    if params["first_fork_m"] >= params["height_m"]:
        problems.append(f"'first_fork_m' = {params['first_fork_m']} must stay under 'height_m' = {params['height_m']}")
    if params["crown_shape"] not in CROWN_SHAPES:
        problems.append(f"'crown_shape' = {params['crown_shape']!r} is not built; the generator builds {', '.join(CROWN_SHAPES)}")
    width = row(params["height_m"])
    if width is None:
        problems.append(f"'height_m' = {params['height_m']} lies in no size row of the sheet "
                        f"({', '.join(f'{h0:g}-{h1:g} m' for (h0, h1), _ in SHEET_ROWS)})")
    elif not width[0] <= params["crown_width_m"] <= width[1]:
        problems.append(f"'crown_width_m' = {params['crown_width_m']} lies outside the sheet's {width[0]:g}-{width[1]:g} m "
                        f"for a tree {params['height_m']:g} m tall")
    for key, idle, why in UNBUILT:
        if params[key] != idle:
            problems.append(f"'{key}' = {params[key]!r} is not built yet ({why}); keep {idle!r}")
    lod = params["lod"]
    if params["impostor"] and not lod["lod2_switch_m"] < lod["impostor_switch_m"] < lod["cull_m"]:
        problems.append(f"'lod.impostor_switch_m' = {lod['impostor_switch_m']} must lie past 'lod.lod2_switch_m' = "
                        f"{lod['lod2_switch_m']} and before 'lod.cull_m' = {lod['cull_m']}")
    return problems


def build(params):
    rng = random.Random(params["seed"])
    report = []
    height, dbh, first_fork, leaf_m = params["height_m"], params["dbh_m"], params["first_fork_m"], params["leaf_m"]
    fork = (params["fork_angle_deg"]["min"], params["fork_angle_deg"]["max"])
    lean = params["lean_deg"] + (STEM_SPLAY_DEG if params["stems"] > 1 else 0.0)
    members = skeleton.trunks(rng, params["stems"], BASE_R_M, dbh, first_fork, params["trunk_sinuosity"], lean)
    tops = [m.points[-1] for m in members]
    axis = (sum(p[0] for p in tops) / len(tops), sum(p[2] for p in tops) / len(tops))
    crown = Crown(axis, first_fork - CROWN_BELOW * (height - first_fork), height, params["crown_width_m"], CARD_MARGIN * leaf_m)
    skeleton.branches(rng, members, params["main_limbs"], params["orders"], params["children_per_order"], fork,
                      params["trunk_sinuosity"], tuple(share * (height - first_fork) for share in LIMB_LENGTH), crown)
    leafy = [(i, m) for i, m in enumerate(members) if m.order >= params["orders"] - 1]
    leafy = [(i, m) for i, m in leafy if rng.random() < params["leaf_cover"]]
    leaves = cards.along(rng, leafy, leaf_m, LEAF_STEP_M, LEAF_START, LEAF_ANGLE_DEG, axis)
    tips = [i for i, m in enumerate(members) if m.order == params["orders"] and rng.random() < params["fruit_share"]]
    pods = {i: cards.hanging(rng, [members[i].points[-1]], POD_M, POD_ASPECT) for i in tips}

    def slot_of(member):
        return TWIG if member.twig else BARK

    leaf_cards = [card for i in sorted(leaves) for card in leaves[i]]
    autumn = random.Random(f"autumn {params['seed']}")  # its own stream: the tree is the same at any share
    is_autumn = [autumn.random() < params["autumn_share"] for _ in leaf_cards]
    by_slot = {LEAF: [c for c, a in zip(leaf_cards, is_autumn) if not a],
               LEAF_AUTUMN: [c for c, a in zip(leaf_cards, is_autumn) if a]}
    pod_pairs = [pods[i] for i in sorted(pods)]
    lods = [Mesh(), Mesh(), Mesh()]
    for lod, mesh in enumerate(lods):
        mesh.smooth = {BARK, TWIG}  # the tubes shade round (module docstring)
        skeleton.tree_tubes(mesh, members, lod, slot_of)
        if lod == 0:  # every leaf in its own order, so the faces are the same at any share, only their slots differ
            for card, fallen in zip(leaf_cards, is_autumn):
                cards.add_to(mesh, [card], LEAF_AUTUMN if fallen else LEAF)
        else:
            for slot, slot_cards in by_slot.items():
                cards.add_to(mesh, cards.thinned(slot_cards, LEAF_KEEP[lod]), slot)
        if lod < 2:
            cards.add_to(mesh, [card for pair in pod_pairs[::lod + 1] for card in pair], POD)
    wood = [(i, m) for i, m in enumerate(members) if not m.twig]
    collision = [meta.capsule(p0, p1, (r0 + r1) / 2, PHYSICS[BARK]) for _, m in wood for p0, p1, r0, r1 in m.segments()]
    twigs = [m for m in members if m.twig]
    report.append(f"tree: {len(members)} members ({params['stems']} trunk{'s' * (params['stems'] > 1)}, "
                  f"{sum(m.order == 1 for m in members)} limbs, {len(wood) - params['stems'] - sum(m.order == 1 for m in members)} "
                  f"branches of 10 mm or more, {len(twigs)} twigs), {len(leaf_cards)} leaves on {len(leafy)} members, "
                  f"{len(pod_pairs)} pod strips" + (f"; {sum(is_autumn)} leaves in leaf_autumn "
                                                    f"({100 * sum(is_autumn) / len(leaf_cards):.1f} %)" if any(is_autumn) else ""))
    report.append(f"capsules: {len(collision)} bark capsules of 10 mm or more (trunk "
                  f"{sum(len(m.segments()) for _, m in wood if m.order == 0)}, limbs "
                  f"{sum(len(m.segments()) for _, m in wood if m.order == 1)}, branches "
                  f"{sum(len(m.segments()) for _, m in wood if m.order >= 2)})")
    reach = cards.card_reach(leaf_m)
    pieces = _shells(members, leaves, pods, audit.card_allowance(reach), report)
    collision += [meta.capsule(a, b, r, PHYSICS[TWIG]) for a, b, r in pieces]
    snag = _records(members)
    report.append("records: " + ", ".join(f"{sum(1 for s in snag if (s['material'], s['kind']) == key)} {key[0]} {key[1]}"
                                          for key in sorted({(s["material"], s["kind"]) for s in snag})) + f", {len(snag)} in all")
    gaps = _gaps(members, collision, snag, report)
    wind = _wind(members, lods[0], dbh, params["leaf_cover"], report)
    lod = params["lod"]
    return {
        "lods": lods,
        "switch_distances_m": [0.0, lod["lod1_switch_m"], lod["lod2_switch_m"]],
        "cull_distance_m": lod["cull_m"],
        "materials": [{"slot": slot, "physics": PHYSICS[slot]} for slot in (BARK, TWIG, LEAF, LEAF_AUTUMN, POD)
                      if slot in lods[0].slots()],
        "collision": collision,
        "gaps": gaps,
        "wind_volumes": wind,
        "snag": snag,
        "card_reach_m": reach,  # a leaf card's far corner from its petiole: audit rule (c)'s allowance less 0.1 m
        "report": report,
        "members": members,  # the skeleton the meshes and the metadata came from (the tests read it)
    }


def _subtrees(members):
    """{index: [indices of the member and every member growing on it]}."""
    below = {}
    for i in reversed(range(len(members))):
        below[i] = [i] + [j for child in members[i].children for j in below[child]]
    return below


def _shell_mesh(members, indices, leaves, pods):
    """The twig, leaf and pod faces of the `indices`' members, as LOD0 builds them."""
    mesh = Mesh()
    for i in indices:
        if members[i].twig:
            skeleton._tube(mesh, list(zip(members[i].points, members[i].radii)), skeleton.TWIG_SIDES, TWIG, cap=False)
    cards.add_to(mesh, [card for i in indices for card in leaves.get(i, ())], LEAF)
    cards.add_to(mesh, [card for i in indices for card in pods.get(i, ())], POD)
    return mesh


def _shells(members, leaves, pods, allowance, report):
    """The twig shell per main limb (review-physics PR5): the limb's twig, leaf and pod faces fitted by the cap by
    steps; a limb that 64 pieces do not pass split into its child subtrees, its own faces one more fit. Reports
    every fit's pieces, kinds, cap, rule (d) count and time, and the tree's."""
    below = _subtrees(members)
    pieces = []
    for limb in (i for i, m in enumerate(members) if m.order == 1):
        fitted = _fit(f"limb {limb}", below[limb], members, leaves, pods, allowance, report)
        if fitted is None:
            continue
        if fitted[1]:
            pieces += fitted[0]
            continue
        report.append(f"shell limb {limb}: 64 pieces do not pass; split into its child subtrees")
        groups = [(f"limb {limb} child {c}", below[c]) for c in members[limb].children] + [(f"limb {limb} own", [limb])]
        for label, indices in groups:
            part = _fit(label, indices, members, leaves, pods, allowance, report)
            if part is not None:
                pieces += part[0]
    count = _neighbourhood(pieces)
    report.append(f"shell tree: {len(pieces)} pieces ({_kinds(pieces)}); most meeting one 2 m cube {count}, "
                  f"limit {audit.SHELL_NEIGHBOURHOOD_MAX}")
    return pieces


def _neighbourhood(pieces):
    """Rule (d)'s count over pieces (a, b, r)."""
    return audit.neighbourhood([audit._Shape(meta.capsule(a, b, r, "")).box() for a, b, r in pieces])[0] if pieces else 0


def _fit(label, indices, members, leaves, pods, allowance, report):
    """One fit of the cap by steps over the members' shell faces: (pieces, passes), or None with no faces."""
    mesh = _shell_mesh(members, indices, leaves, pods)
    if not mesh.faces:
        return None
    started = time.perf_counter()
    pieces, cap, passes = shell.fit(mesh, (TWIG,), (LEAF, POD), allowance)
    report.append(f"shell {label}: {len(pieces)} pieces ({_kinds(pieces)}) at cap {cap}, {'passes' if passes else 'FAILS'} "
                  f"(b) and (c); most meeting one 2 m cube {_neighbourhood(pieces)}; fit {time.perf_counter() - started:.1f} s")
    return pieces, passes


def _kinds(pieces):
    spheres = sum(1 for a, b, _ in pieces if a == b)
    return f"{len(pieces) - spheres} capsules, {spheres} spheres"


def _records(members):
    """The sheet's snag rule (design decision 4, review-physics PR4 and PR6): per wood member, an `edge` on every
    segment under SNAG_EDGE_M but its top one and a `hook` on the top one to its free tip, each at its capsule's
    radius in `bark`; per twig one `hook` through its rings at its mean radius in `twig`."""
    records = []
    for m in members:
        if m.twig:
            records.append({"kind": "hook", "points": [list(p) for p in m.points], "radius_m": (m.radii[0] + m.radii[-1]) / 2,
                            "material": PHYSICS[TWIG]})
            continue
        segments = m.segments()
        for k, (p0, p1, r0, r1) in enumerate(segments):
            r = (r0 + r1) / 2
            if k == len(segments) - 1 or 2 * r < SNAG_EDGE_M:
                records.append({"kind": "hook" if k == len(segments) - 1 else "edge", "points": [list(p0), list(p1)],
                                "radius_m": r, "material": PHYSICS[BARK]})
    return records


# --- gaps ---------------------------------------------------------------------------------------------------------

def _pairs(members):
    """(label, A, B, ground) for every two members of GAP_MEMBER_M or more leaving one fork: A and B as
    (points, radii, first child arc) from the fork on, `ground` when they leave the ground side by side."""
    def run(m, s=0.0):
        """The member from arc s on: its points and radii with s's point first, and the arc to its next child."""
        point, _, radius = m.at(s)
        arcs, acc = [], 0.0
        for l in m.lengths():
            acc += l
            arcs.append(acc)
        points = [point] + [p for p, a in zip(m.points[1:], arcs) if a > s + 1e-9]
        radii = [radius] + [r for r, a in zip(m.radii[1:], arcs) if a > s + 1e-9]
        nexts = [members[c].attach - s for c in m.children if members[c].attach > s + 1e-9]
        return points, radii, min(nexts) if nexts else m.length() - s

    out = []
    trunks = [i for i, m in enumerate(members) if m.order == 0]
    ring = sorted(trunks, key=lambda i: math.atan2(members[i].points[0][2], members[i].points[0][0]))
    for a, b in zip(ring, ring[1:] + ring[:1]) if len(ring) > 2 else ([ring[:2]] if len(ring) == 2 else []):
        if 2 * min(members[a].radii[0], members[b].radii[0]) >= GAP_MEMBER_M:
            out.append((f"stems {a}-{b}", run(members[a]), run(members[b]), True))
    for t in trunks:
        limbs = members[t].children
        top, tangent = members[t].points[-1], skeleton.normalize(skeleton.sub(members[t].points[-1], members[t].points[-2]))
        e1 = skeleton.normalize(skeleton.cross(tangent, skeleton._helper(tangent)))
        e2 = skeleton.cross(tangent, e1)

        def heading(i):
            d = skeleton.sub(members[i].points[1], top)
            return math.atan2(skeleton.dot(d, e2), skeleton.dot(d, e1))
        ring = sorted(limbs, key=heading)
        for a, b in zip(ring, ring[1:] + ring[:1]) if len(ring) > 2 else ([ring[:2]] if len(ring) == 2 else []):
            if 2 * min(members[a].radii[0], members[b].radii[0]) >= GAP_MEMBER_M:
                out.append((f"fork {t} limbs {a}-{b}", run(members[a]), run(members[b]), False))
    for i, m in enumerate(members):
        if m.order >= 2 and 2 * m.radii[0] >= GAP_MEMBER_M:
            parent = members[m.parent]
            if 2 * parent.at(m.attach)[2] >= GAP_MEMBER_M:
                out.append((f"branch {m.parent}-{i}", run(parent, m.attach), run(m), False))
    return out


def _gaps(members, collision, snag, report):
    """The written gaps (module docstring): each pair's polygon at the largest closing chord that keeps it clear,
    kept when its largest inscribed circle is GAP_MIN_M or more, reported with the drone's verdict."""
    shapes = [audit._Shape(c) for c in collision]
    segments = [(s.place((0.0, -s.h, 0.0)), s.place((0.0, s.h, 0.0)), s.r) for s in shapes]
    segments += [(tuple(p), tuple(q), rec["radius_m"]) for rec in snag if rec["material"] == PHYSICS[TWIG]
                 for p, q in zip(rec["points"], rec["points"][1:])]
    gaps, pairs = [], _pairs(members)
    for label, a, b, ground in pairs:
        found = _opening(a, b, ground, segments)
        if found is None:
            continue
        polygon, normal, narrowest = found
        verdict = "any yaw" if narrowest >= ANY_YAW_M else "nose-first" if narrowest >= NOSE_FIRST_M else "too narrow to pass"
        if narrowest < GAP_MIN_M:
            report.append(f"gap {label}: largest inscribed circle {narrowest:.3f} m, under {GAP_MIN_M} m, not written")
            continue
        gaps.append({"id": f"g{len(gaps)}", "polygon": polygon, "normal": list(normal), "narrowest_m": narrowest})
        report.append(f"gap g{len(gaps) - 1} ({label}): narrowest {narrowest:.3f} m, {verdict}, {len(polygon)} vertices")
    report.append(f"gaps: {len(gaps)} written of {len(pairs)} forks of two members of 30 mm or more")
    return gaps


def _opening(a, b, ground, segments):
    """(polygon, normal, narrowest) of the opening between A and B (see _pairs), or None when they leave parallel:
    in the plane through their start and first headings (a clump's stems: through both bases and their mean heading),
    bounded by their surfaces offset GAP_MARGIN_M inward and by the chord at the share lam of each one's arc to its
    next child fork, lam as large as keeps every sample of GAP_SAMPLE_M within it farther than GAP_GUARD_M from every
    shape `segments` [(p, q, r)]: the capsules, shell pieces and twig records."""
    (pa, ra, sa), (pb, rb, sb) = a, b
    da, db = skeleton.normalize(skeleton.sub(pa[1], pa[0])), skeleton.normalize(skeleton.sub(pb[1], pb[0]))
    origin = skeleton.scale(skeleton.add(pa[0], pb[0]), 0.5)
    mean = skeleton.add(da, db)
    normal = skeleton.cross(skeleton.sub(pb[0], pa[0]), mean) if ground else skeleton.cross(da, db)
    if skeleton.norm(normal) < 1e-6 or skeleton.norm(mean) < 1e-6:
        return None
    normal = skeleton.normalize(normal)
    e1 = skeleton.normalize(skeleton.sub(mean, skeleton.scale(normal, skeleton.dot(mean, normal))))
    e2 = skeleton.cross(normal, e1)

    def flat(p):
        q = skeleton.sub(p, origin)
        return (skeleton.dot(q, e1), skeleton.dot(q, e2))

    def lift(x, y):
        return skeleton.add(origin, skeleton.add(skeleton.scale(e1, x), skeleton.scale(e2, y)))

    fa, fb = [flat(p) for p in pa], [flat(p) for p in pb]
    side_a = 1.0 if sum(y for _, y in fa[1:]) > sum(y for _, y in fb[1:]) else -1.0  # A above B in e2, or below
    inner_a, inner_b = _offset(fa, ra, -side_a), _offset(fb, rb, side_a)
    arcs_a, arcs_b = _arcs(pa), _arcs(pb)
    if ground:  # side by side from the ground: the base edge runs along it between their inner surfaces
        base_a, base_b, start_a, start_b = [inner_a[0]], [inner_b[0]], 0.0, 0.0
    else:  # from one fork: the polygon starts where their inner surfaces cross, the crotch
        crotch = _cross_lines(inner_a[0], inner_a[1], inner_b[0], inner_b[1])
        if crotch is None or crotch[0] <= 0:
            return None
        base_a, base_b = [crotch], []
        start_a, start_b = _along(inner_a, crotch), _along(inner_b, crotch)

    def polygon(lam):
        return base_a + _cut(inner_a, arcs_a, start_a, lam * sa) + _cut(inner_b, arcs_b, start_b, lam * sb)[::-1] + base_b

    whole = polygon(1.0)
    if len(whole) < 3:
        return None
    if _simple(whole) and 2 * audit._inscribed_radius(whole) < GAP_MIN_M:
        return None  # a nearer chord only narrows it
    lo = [min(p[k] for p in whole) for k in (0, 1)]
    hi = [max(p[k] for p in whole) for k in (0, 1)]
    corners = [lift(x, y) for x in (lo[0], hi[0]) for y in (lo[1], hi[1])]
    box_lo = [min(c[k] for c in corners) - GAP_GUARD_M for k in range(3)]
    box_hi = [max(c[k] for c in corners) + GAP_GUARD_M for k in range(3)]
    near = [(p, q, r) for p, q, r in segments
            if all(min(p[k], q[k]) - r <= box_hi[k] and max(p[k], q[k]) + r >= box_lo[k] for k in range(3))]
    blocked = []
    for i in range(int((hi[0] - lo[0]) / GAP_SAMPLE_M) + 2):
        for j in range(int((hi[1] - lo[1]) / GAP_SAMPLE_M) + 2):
            x, y = lo[0] + i * GAP_SAMPLE_M, lo[1] + j * GAP_SAMPLE_M
            if audit._inside_polygon2((x, y), whole):
                point = lift(x, y)
                if any(math.sqrt(shell._segment_distance2(point, p, q)) - r <= GAP_GUARD_M for p, q, r in near):
                    blocked.append((x, y))

    def clear(lam):
        poly = polygon(lam)
        return len(poly) >= 3 and _simple(poly) and not any(audit._inside_polygon2(p, poly) for p in blocked)

    lam = 1.0
    if not clear(lam):
        low, high = 0.0, 1.0
        for _ in range(GAP_STEPS):
            mid = (low + high) / 2
            low, high = (mid, high) if clear(mid) else (low, mid)
        lam = low
    poly = polygon(lam)
    if lam <= 0.0 or not clear(lam):
        return None
    return [list(lift(x, y)) for x, y in poly], normal, 2 * audit._inscribed_radius(poly)


def _offset(points, radii, toward):
    """The 2D polyline `points` moved off itself by each point's radius plus GAP_MARGIN_M, to the side whose e2
    component has the sign of `toward`, each point along the mean normal of the segments that meet there."""
    normals = []
    for a, b in zip(points, points[1:]):
        dx, dy = b[0] - a[0], b[1] - a[1]
        length = math.hypot(dx, dy) or 1.0
        n = (-dy / length, dx / length)
        normals.append(n if n[1] * toward >= 0 else (-n[0], -n[1]))
    out = []
    for k, p in enumerate(points):
        near = normals[max(0, k - 1):k + 1]
        nx, ny = sum(n[0] for n in near), sum(n[1] for n in near)
        length = math.hypot(nx, ny) or 1.0
        d = radii[k] + GAP_MARGIN_M
        out.append((p[0] + d * nx / length, p[1] + d * ny / length))
    return out


def _arcs(points):
    """The arc length at each point of a 3D polyline."""
    out = [0.0]
    for a, b in zip(points, points[1:]):
        out.append(out[-1] + skeleton.norm(skeleton.sub(b, a)))
    return out


def _cross_lines(p1, p2, q1, q2):
    """Where the 2D lines p1-p2 and q1-q2 cross, or None when they run parallel."""
    d1, d2 = (p2[0] - p1[0], p2[1] - p1[1]), (q2[0] - q1[0], q2[1] - q1[1])
    det = d1[0] * d2[1] - d1[1] * d2[0]
    if abs(det) < 1e-12:
        return None
    t = ((q1[0] - p1[0]) * d2[1] - (q1[1] - p1[1]) * d2[0]) / det
    return (p1[0] + t * d1[0], p1[1] + t * d1[1])


def _along(inner, point):
    """The share of the first segment of `inner` at which `point`, on its line, lies (may run past either end)."""
    (x0, y0), (x1, y1) = inner[0], inner[1]
    dx, dy = x1 - x0, y1 - y0
    return ((point[0] - x0) * dx + (point[1] - y0) * dy) / (dx * dx + dy * dy)


def _cut(inner, arcs, start, end):
    """The points of `inner` (offset from a polyline whose arc at each point is `arcs`) after the share `start` of
    its first segment, up to the arc `end`, interpolated there; empty when `end` comes first."""
    first = start * arcs[1]
    if end <= first:
        return []
    out = [p for p, s in zip(inner, arcs) if first < s < end]
    for k in range(len(arcs) - 1):
        if arcs[k] <= end <= arcs[k + 1] or k == len(arcs) - 2:
            t = min(1.0, (end - arcs[k]) / (arcs[k + 1] - arcs[k]))
            out.append((inner[k][0] + t * (inner[k + 1][0] - inner[k][0]), inner[k][1] + t * (inner[k + 1][1] - inner[k][1])))
            break
    return out


def _simple(polygon):
    """No two edges of the 2D polygon that do not share a vertex cross or touch."""
    edges = list(zip(polygon, polygon[1:] + polygon[:1]))
    for i, (a, b) in enumerate(edges):
        for j in range(i + 2, len(edges)):
            if i == 0 and j == len(edges) - 1:
                continue
            c, d = edges[j]
            if _segments_meet(a, b, c, d):
                return False
    return True


def _segments_meet(a, b, c, d):
    def orient(p, q, r):
        return (q[0] - p[0]) * (r[1] - p[1]) - (q[1] - p[1]) * (r[0] - p[0])
    o1, o2, o3, o4 = orient(a, b, c), orient(a, b, d), orient(c, d, a), orient(c, d, b)
    return (o1 * o2 <= 0) and (o3 * o4 <= 0)


# --- wind ---------------------------------------------------------------------------------------------------------

IDENTITY = [0.0, 0.0, 0.0, 1.0]
WIND_CLEARANCE_M = 0.001    # a trunk column's highest corner stands this under the crown box: never overlapping


def _wind(members, lod0, dbh, leaf_cover, report):
    """One crown box over the LOD0 twig, leaf and pod vertices at the state's porosity, and per trunk a solid column
    of width dbh along its chord from the ground to WIND_CLEARANCE_M under the box (review-physics PR8)."""
    crown = {lod0.vertices[i] for indices, _, _, slot in lod0.faces if slot in (TWIG, LEAF, LEAF_AUTUMN, POD) for i in indices}
    if not crown:
        return []
    lo, hi = ([f(v[a] for v in crown) for a in range(3)] for f in (min, max))
    volumes = [{"shape": "box", "center": [(lo[a] + hi[a]) / 2 for a in range(3)], "size": [hi[a] - lo[a] for a in range(3)],
                "rotation": IDENTITY, "porosity": porosity(leaf_cover)}]
    for trunk in (m for m in members if m.order == 0):
        base = trunk.points[0]
        heading = skeleton.normalize(skeleton.sub(trunk.points[-1], base))
        q = meta.rotation_from_y(heading)
        u, v = audit._rotate(q, (1.0, 0.0, 0.0)), audit._rotate(q, (0.0, 0.0, 1.0))
        length = (lo[1] - WIND_CLEARANCE_M - dbh / 2 * (abs(u[1]) + abs(v[1])) - base[1]) / heading[1]
        if length > 0:
            volumes.append({"shape": "box", "center": list(skeleton.add(base, skeleton.scale(heading, length / 2))),
                            "size": [dbh, length, dbh], "rotation": q, "porosity": 0.0})
    report.append(f"wind: crown box y {lo[1]:.3f}-{hi[1]:.3f} m at porosity {porosity(leaf_cover):.3f}; "
                  f"{len(volumes) - 1} trunk column{'s' * (len(volumes) > 2)} of {dbh:g} m at porosity 0 below it")
    return volumes
