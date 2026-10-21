"""Leaf cards for the vegetation generators (p1-v13-shrub, design decisions 2 and 3): double-sided quads along the
twigs, each mapped into one sprig cell of the LeafSet002 atlas, and the far LOD's crossed cards. Plain Python in
asset space, drawing from the caller's seeded random.Random.

A card is a sprig, a short leafy shoot: `card_m` long and as wide as its cell's aspect, its stalk end on the twig
at a parameter biased toward the tip (t = 1 - u^2, the leaves sit outside), leaving the twig at the caller's fork
angle at a random azimuth about it (0 lays it along the twig; the shrub passes the sheet's fork_angle_deg, so the
sprigs make a leafy mass round the twig instead of a frond along it, seen in the 1.3 look task against letter H);
second-order twigs draw LEVEL_WEIGHT times as many cards as first-order ones. The leaf recipe is two-sided and
alpha-clipped, so one quad, two triangles, shows both faces, and the Fpv450/Leaf shader lights a back face with its
own normal. A card faces outward: its normal is the clump's outward horizontal direction at the card, made
perpendicular to the sprig, or up when the sprig itself points outward, rolled about the sprig within ROLL_JITTER
(more of a card's area faces a viewer outside the clump, so the clump reads opaque with fewer cards). A card whose
corners would dip under the ground is lifted onto it.

The far LOD is CROSSED vertical cards of the whole clump, each mapped to a dense box of one sprig, FAR_PX, the
atlas scaled up: the whole atlas is 20 % leaf, so at the 9 px of a far clump its six thin sprigs fell under the
clip and the card vanished (measured on the first review sheet at 200 m). Each card is the ellipse that covers what
LOD0's leaves and wood cover seen square to it (`outline`), and its box's colour is the leaves' mean, so LOD2 neither
pops brighter nor grows at its switch (p1-v01-locust task 1.2b). Its faces are lit with normals of their own, not
their planes': each card is two faces back to back on a one-sided material (the leaf shader would turn a back face's
normal round, so one leaning up would lean down), each face's normal its side's horizontal leaning up as the leaves'
mean visible normal does from the review's far look-down (`visible_lean`, task 1.2j: a vertical plane has no normal
towards the sun's half vector and took 35-40 % less of the leaves' specular). The nearer LODs keep one card for
every two or every four of near facing, scaled to hold all the cards' silhouette and that of the thin wood the LOD
drops (`thinned`).

Trees (p1-v01-locust, design decision 2): `along` lays a pinnate leaf card every step along a member's polyline,
alternate sides, each a whole sprig cell (a stalk with 12-20 small oval leaves, which at a 0.25 m leaf are the
locust's 2.5-6.8 cm leaflets on their rachis); `hanging` hangs a crossed pair of pod cards from a twig tip.

The cells are LeafSet002's six sprigs, measured on its 2048 px map (design 3, "as built") as inclusive pixel
columns and rows, row 0 at the top, so V runs up and the stalk sits at the cell's V0; the recipe tiles the atlas at
1.0 m, so UV metres are atlas fractions.
"""
import math

from .skeleton import AXES, add, cross, dot, norm, normalize, scale, sub

SET_PX = 2048
CELLS_PX = (((192, 128), (560, 960)), ((784, 128), (1264, 960)), ((1472, 128), (1856, 960)),
            ((192, 1152), (560, 1952)), ((784, 1152), (1264, 1952)), ((1472, 1152), (1856, 1952)))
# The far cards' boxes, 128 x 256 px in cells 5 and 1, 90 % of their texels opaque (alpha >= 0.5) so they hold at a
# far mip, whose colour is the leaves' mean: the opaque texels' linear colour times the mask's occlusion over the
# six cells, each weighted by its opaque texels as a card draws a cell at random, is luminance 0.249 with r/g 0.751
# and b/g 0.224; the boxes come to +1.2 and +3.9 % of it, r/g 0.758 and 0.738, b/g 0.170 and 0.277, the closest of
# every such box of the atlas (measured on leaf_shrub_base.png and leaf_shrub_mask.png; p1-v01-locust task 1.2b,
# where the densest cores task 1.3 of p1-v13-shrub took came out 26-42 % bright by day against LOD0).
FAR_PX = (((1696, 1184), (1823, 1439)), ((936, 680), (1063, 935)))
FAR_OPAQUE = 0.9            # the boxes' opaque share: a far card covers this of its outline
FAR_SIDES = 12              # a far card's outline, a polygon round its ellipse
FAR_GRID_M = 0.05           # the raster LOD0's leaves are measured on for the far cards
FAR_LOOK_DOWN_DEG = 20.0    # the far cards' normals are the leaves' seen from this look-down: the review's far shots'
# The share of each cell's texels at alpha 0.5 or more (measured on leaf_shrub_base.png; leaf_locust_base.png is the
# same map): what of a card's area its leaves cover.
CELL_OPAQUE = (0.394, 0.406, 0.453, 0.518, 0.305, 0.499)
# The cells a thinned LOD's cards take in turn: cells 1 and 5, whose opaque texels' mean linear colour, (0.2095,
# 0.2887, 0.0758) and (0.2201, 0.2767, 0.0533), average to the six cells' (0.2117, 0.2826, 0.0634) within 2 %, where
# single cells run from 0.133 to 0.335 in luminance (measured on leaf_shrub_base.png; p1-v01-locust task 1.2b).
MEAN_CELLS = (1, 5)
CROSSED = 3
LEVEL_WEIGHT = {1: 1, 2: 3}
MIN_T = 0.15
ROLL_JITTER = 0.6       # radians either side of facing outward (assumed: leaves face the light, not all one way)
MIN_OUTWARD = 0.25      # sine of the angle between the twig and the outward direction under which a card faces up
UP = (0.0, 1.0, 0.0)


def cell_uv(cell_px, set_px=SET_PX):
    """A pixel box of the atlas as (min, max) UV corners: row 0 is the top of the map, V runs up."""
    (x0, y0), (x1, y1) = cell_px
    return ((x0 / set_px, 1 - (y1 + 1) / set_px), ((x1 + 1) / set_px, 1 - y0 / set_px))


CELLS_UV = tuple(cell_uv(c) for c in CELLS_PX)


def card_reach(card_m):
    """How far a card `card_m` long reaches from its stalk end: its far corners, card_m * sqrt(1 + (aspect / 2)^2) at
    the widest of the cells it may map (`_card` lays it as wide as its cell's aspect, centred on its axis), 1.044 *
    card_m at cell 4's 0.600 (p1-v01-locust review-physics §A.3: audit rule (c)'s card reach)."""
    return card_m * max(math.hypot(1.0, (u1 - u0) / (v1 - v0) / 2) for (u0, v0), (u1, v1) in CELLS_UV)


class Card:
    """A quad: `corners` in cyclic order (stalk end first), one face `normal`, the `uvs` per corner, its `cell`; a far
    card's face also its corners' shading `normals`."""

    def __init__(self, corners, normal, uvs, cell, normals=None):
        self.corners, self.normal, self.uvs, self.cell, self.normals = corners, normal, uvs, cell, normals


def place(rng, twigs, count, card_m, ground_y=0.0, fork_deg=(0.0, 0.0)):
    """`count` cards over the `twigs` (lib.skeleton.Twig), each `card_m` long, leaving its twig at an angle drawn
    from `fork_deg` (lo, hi) degrees."""
    if not twigs or count <= 0:
        return []
    weights = [LEVEL_WEIGHT.get(twig.level, 1) for twig in twigs]
    cards = []
    for index in rng.choices(range(len(twigs)), weights=weights, k=count):
        twig = twigs[index]
        u = rng.random()
        t = max(MIN_T, 1 - u * u)
        attach = add(twig.p0, scale(sub(twig.p1, twig.p0), t))
        axis = _off_twig(normalize(sub(twig.p1, twig.p0)), math.radians(rng.uniform(*fork_deg)), rng.uniform(0.0, 2 * math.pi))
        cards.append(_card(rng, attach, axis, card_m, ground_y))
    return cards


def along(rng, members, leaf_m, step_m, start, angle_deg, axis=(0.0, 0.0), ground_y=0.0):
    """Pinnate leaf cards along polyline members (lib.skeleton.Member; p1-v01-locust, design decision 2): one every
    `step_m` of arc from `start` of a member's length to its tip, each a whole cell of the atlas `leaf_m` long from its
    petiole on the member, alternate sides about it (half a turn apart, jittered) at an angle drawn from `angle_deg`,
    facing outward from the crown's vertical `axis` (x, z) as the sprigs do. Returns {member index: [Card]}."""
    out = {}
    for index, member in members:
        length = member.length()
        phase, cards = rng.uniform(0.0, 2 * math.pi), []
        k, s = 0, start * length
        while s <= length:
            p, tangent, _ = member.at(s)
            leaf = _off_twig(tangent, math.radians(rng.uniform(*angle_deg)), phase + k * math.pi + rng.uniform(-0.4, 0.4))
            cards.append(_card(rng, p, leaf, leaf_m, ground_y, axis))
            k, s = k + 1, s + step_m
        out[index] = cards
    return out


def hanging(rng, tips, length, aspect, uv=((0.0, 0.0), (1.0, 1.0))):
    """Two crossed vertical cards hanging `length` from each tip point, `aspect` as wide as long, at a random
    azimuth, mapped over the whole of `uv`: the pod strips (design decision 2)."""
    (u0, v0), (u1, v1) = uv
    cards = []
    for tip in tips:
        azimuth = rng.uniform(0.0, math.pi)
        for k in (0, 1):
            a = azimuth + k * math.pi / 2
            b = scale((math.cos(a), 0.0, math.sin(a)), length * aspect / 2)
            low = (tip[0], tip[1] - length, tip[2])
            corners = [sub(low, b), add(low, b), add(tip, b), sub(tip, b)]
            cards.append(Card(corners, cross(UP, normalize(b)), [(u0, v0), (u1, v0), (u1, v1), (u0, v1)], None))
    return cards


def _card(rng, attach, axis, card_m, ground_y, crown=(0.0, 0.0)):
    """One card `card_m` long from `attach` along the unit `axis`, as wide as its cell's aspect, facing outward from
    the crown's vertical axis with a rolled jitter, lifted onto the ground if it dips under it."""
    half_l = card_m / 2
    centre = add(attach, scale(axis, half_l))
    side = cross(_facing(axis, centre, rng.uniform(-ROLL_JITTER, ROLL_JITTER), crown), axis)
    cell = rng.randrange(len(CELLS_UV))
    (u0, v0), (u1, v1) = CELLS_UV[cell]
    half_w = half_l * (u1 - u0) / (v1 - v0)
    a, b = scale(axis, half_l), scale(side, half_w)
    corners = [sub(sub(centre, a), b), add(sub(centre, a), b), add(add(centre, a), b), sub(add(centre, a), b)]
    lift = ground_y - min(c[1] for c in corners)
    if lift > 0:
        corners = [(x, y + lift, z) for x, y, z in corners]
    return Card(corners, cross(axis, side), [(u0, v0), (u1, v0), (u1, v1), (u0, v1)], cell)


def crossed(leaves, wood=(), count=CROSSED):
    """The far LOD of the leaf `leaves` and the `wood` it drops, segments (p0, p1, r0, r1) (p1-v01-locust task
    1.2b): `count` vertical cards crossed at even azimuths about the vertical through the leaves' centre, card k a
    FAR_SIDES-gon round the ellipse that stands for them seen square to it (`outline`), mapped over the box
    FAR_PX[k mod 2] (the box's frame on the ellipse's). So it covers what LOD0's leaves and wood cover there, where
    the leaves are, in the leaves' colour. Each card is two faces back to back, for a one-sided material, each
    shaded with one normal of its own (task 1.2j): its side's horizontal, leaning up by the leaves' visible lean
    (`visible_lean`), so the sun and the sky light it as they light the leaves the camera sees, not as they light a
    vertical plane. None without leaves."""
    if not leaves:
        return None
    lean = visible_lean(leaves)
    points = [c for card in leaves for c in card.corners]
    axis = (sum(p[0] for p in points) / len(points), sum(p[2] for p in points) / len(points))
    cards = []
    for k in range(count):
        azimuth = math.pi * k / count
        across = (math.cos(azimuth), 0.0, math.sin(azimuth))
        (cu, cv), (a, b) = outline(leaves, wood, axis, across)
        (u0, v0), (u1, v1) = cell_uv(FAR_PX[k % len(FAR_PX)])
        corners, uvs = [], []
        for j in range(FAR_SIDES):
            t = 2 * math.pi * (j + 0.5) / FAR_SIDES
            x, y = math.cos(t), math.sin(t)
            corners.append((axis[0] + across[0] * (cu + a * x), cv + b * y, axis[1] + across[2] * (cu + a * x)))
            uvs.append((u0 + (x + 1) / 2 * (u1 - u0), v0 + (y + 1) / 2 * (v1 - v0)))
        plane = cross(UP, across)
        for side in (plane, scale(plane, -1.0)):
            normal = add(scale(side, math.cos(lean)), scale(UP, math.sin(lean)))
            cards.append(Card(corners, side, uvs, None, [normal] * FAR_SIDES))
    return cards


def wedges(far, share):
    """(summer, autumn): the far cards of a crown mixing two leaf recipes (p1-v01-locust task 1.5). Without a mix (share
    0 or 1) the cards as they are, all in one list. Otherwise each face is cut into FAR_SIDES triangles about its
    centre (the box's centre in UV, its corners' normal), and the wedges of each card go to the autumn list where the
    running count of `share` over them steps up, from an offset that turns by the golden ratio card by card: about
    `share` of every card's wedges, evenly spread, the same on both its faces."""
    if share <= 0.0:
        return list(far), []
    if share >= 1.0:
        return [], list(far)
    summer, autumn = [], []
    for k, card in enumerate(far):
        offset = (k // 2) * 0.6180339887498949 % 1.0  # the two faces of a card share it
        centre = scale(tuple(sum(c[a] for c in card.corners) for a in range(3)), 1.0 / len(card.corners))
        uv = tuple(sum(uv[a] for uv in card.uvs) / len(card.uvs) for a in range(2))
        n = len(card.corners)
        for j in range(n):
            wedge = Card([centre, card.corners[j], card.corners[(j + 1) % n]], card.normal,
                         [uv, card.uvs[j], card.uvs[(j + 1) % n]], None,
                         [card.normals[0]] * 3 if card.normals else None)
            (autumn if math.floor(offset + (j + 1) * share) > math.floor(offset + j * share) else summer).append(wedge)
    return summer, autumn


def visible_lean(leaves, down_deg=FAR_LOOK_DOWN_DEG, azimuths=8):
    """The lean above the horizontal, radians, of the leaf cards' mean visible normal seen from `down_deg` above the
    horizon, averaged over `azimuths` even azimuths: each card's normal turned to the viewer, as the leaf shader lights
    a back face with its own normal, and weighted by its opaque area seen from there; the lean of the mean is its
    angle above the horizontal towards the viewer."""
    down = math.radians(down_deg)
    total = 0.0
    for k in range(azimuths):
        az = 2 * math.pi * (k + 0.5) / azimuths
        towards = (math.sin(az), 0.0, math.cos(az))
        view = add(scale(towards, math.cos(down)), scale(UP, math.sin(down)))
        mean = (0.0, 0.0, 0.0)
        for card in leaves:
            n = normalize(card.normal)
            a, b, c, _ = card.corners
            mean = add(mean, scale(n, norm(cross(sub(b, a), sub(c, b))) * CELL_OPAQUE[card.cell] * dot(n, view)))
        total += math.atan2(mean[1], dot(mean, towards))
    return total / azimuths


def outline(leaves, wood, axis, across):
    """The ellipse that stands for the leaf cards and the wood segments seen square to the vertical plane through
    `axis` (x, z) along the horizontal `across`: ((centre along across, centre height), (half width, half height)).
    Both are projected on the plane (a segment as the strip of its diameters) and rasterised at FAR_GRID_M, a card
    covering its cell's CELL_OPAQUE share of the points inside it and wood all of them (a point under pieces of shares
    o_i is covered 1 - prod(1 - o_i)); the ellipse has the centre of the leaves' coverage and the shape of a uniform
    ellipse of its second moments (half axes twice the deviations), scaled so that FAR_OPAQUE of its area is the area
    the leaves and the wood cover together."""
    g = FAR_GRID_M
    pieces = [([(_dot2(c, axis, across), c[1]) for c in card.corners], CELL_OPAQUE[card.cell], True) for card in leaves]
    for p0, p1, r0, r1 in wood:
        (u0, v0), (u1, v1) = (_dot2(p0, axis, across), p0[1]), (_dot2(p1, axis, across), p1[1])
        length = math.hypot(u1 - u0, v1 - v0)
        if length > 1e-9:
            nu, nv = (v0 - v1) / length, (u1 - u0) / length
            pieces.append(([(u0 + nu * r0, v0 + nv * r0), (u1 + nu * r1, v1 + nv * r1), (u1 - nu * r1, v1 - nv * r1),
                            (u0 - nu * r0, v0 - nv * r0)], 1.0, False))
    lo_u = min(u for q, _, _ in pieces for u, _ in q)
    lo_v = min(v for q, _, _ in pieces for _, v in q)
    clear, clear_leaves = {}, {}
    for quad, share, leaf in pieces:
        us, vs = [u for u, _ in quad], [v for _, v in quad]
        for i in range(math.ceil((min(us) - lo_u) / g - 0.5), math.floor((max(us) - lo_u) / g - 0.5) + 1):
            for j in range(math.ceil((min(vs) - lo_v) / g - 0.5), math.floor((max(vs) - lo_v) / g - 0.5) + 1):
                if _inside(quad, (lo_u + (i + 0.5) * g, lo_v + (j + 0.5) * g)):
                    clear[i, j] = clear.get((i, j), 1.0) * (1.0 - share)
                    if leaf:
                        clear_leaves[i, j] = clear_leaves.get((i, j), 1.0) * (1.0 - share)
    covered = sum(1.0 - c for c in clear.values())
    weight = {key: 1.0 - c for key, c in clear_leaves.items()}
    total = sum(weight.values())
    mu = sum(w * (lo_u + (i + 0.5) * g) for (i, _), w in weight.items()) / total
    mv = sum(w * (lo_v + (j + 0.5) * g) for (_, j), w in weight.items()) / total
    su = math.sqrt(sum(w * (lo_u + (i + 0.5) * g - mu) ** 2 for (i, _), w in weight.items()) / total + g * g / 12)
    sv = math.sqrt(sum(w * (lo_v + (j + 0.5) * g - mv) ** 2 for (_, j), w in weight.items()) / total + g * g / 12)
    a, b = 2 * su, 2 * sv
    fit = math.sqrt(covered * g * g / (FAR_OPAQUE * math.pi * a * b))
    return (mu, mv), (a * fit, b * fit)


def _dot2(p, axis, across):
    return (p[0] - axis[0]) * across[0] + (p[2] - axis[1]) * across[2]


def _inside(polygon, point):
    """Whether the point lies in the convex polygon (either winding), its edges included."""
    sign = 0
    for (x0, y0), (x1, y1) in zip(polygon, polygon[1:] + polygon[:1]):
        side = (x1 - x0) * (point[1] - y0) - (y1 - y0) * (point[0] - x0)
        if side:
            if sign and (side > 0) != (sign > 0):
                return False
            sign = side
    return True


def silhouette(cards):
    """The cards' mean silhouette, m2: half the opaque area of each (a flat card seen from every side shows half its
    area on average), its cell's CELL_OPAQUE share of it (a cell-less card, a pod strip, whole)."""
    total = 0.0
    for card in cards:
        a, b, c, _ = card.corners
        area = norm(cross(sub(b, a), sub(c, b)))
        total += area * (CELL_OPAQUE[card.cell] if card.cell is not None else 1.0) / 2
    return total


def thinned(cards, keep, dropped_m2=0.0, ground_y=0.0):
    """One card for every `keep` of near facing, standing for them (p1-v01-locust tasks 1.2b and 1.6; `by_facing`),
    so the kept cards face the light and the view as all the cards do: kept as every `keep`-th card in the twigs'
    order, V01_S_autumn's LOD2 kept 13 of its 52 yellow leaves, which faced the review's sun 86 % more than the 52, and
    its LOD2 switch read 6.52 % bright by day. Each is mapped to the MEAN_CELLS in turn (as long as before, as wide as
    its new cell), so the kept cards show the leaves' mean colour rather than the luck of the cells they drew, and
    scaled about its centre so that their silhouette (`silhouette`) is all the cards' plus `dropped_m2`, the mean
    silhouette of what the LOD drops besides (its thin wood): a far LOD keeps its crown's coverage and colour. A card
    dipping under the ground is lifted onto it; `keep` 1 returns the cards as they are."""
    if keep == 1 and not dropped_m2:
        return list(cards)
    kept = [_recelled(card, MEAN_CELLS[i % len(MEAN_CELLS)]) for i, card in enumerate(by_facing(cards, keep))]
    s = math.sqrt((silhouette(cards) + dropped_m2) / silhouette(kept)) if kept else 1.0
    out = []
    for card in kept:
        centre = scale(tuple(sum(c[a] for c in card.corners) for a in range(3)), 1.0 / len(card.corners))
        corners = [add(centre, scale(sub(c, centre), s)) for c in card.corners]
        lift = ground_y - min(c[1] for c in corners)
        if lift > 0:
            corners = [(x, y + lift, z) for x, y, z in corners]
        out.append(Card(corners, card.normal, card.uvs, card.cell))
    return out


def by_facing(cards, keep):
    """ceil(n / keep) of the cards, one for each group of about `keep` cards of near facing, in the cards' order: the
    groups come from halving the cards at the median of their normals along the axis those spread widest on, each
    half taking its share of the groups, until a group is one; each group keeps the card nearest its mean normal
    (task 1.6: a stratified sample of the facings, whatever order the twigs laid the cards in)."""
    picked = []

    def split(group, count):
        if count == 1:
            mean = tuple(sum(cards[i].normal[a] for i in group) for a in range(3))
            picked.append(max(group, key=lambda i: (dot(cards[i].normal, mean), -i)))
            return
        axis = max(range(3), key=lambda a: max(cards[i].normal[a] for i in group) - min(cards[i].normal[a] for i in group))
        group = sorted(group, key=lambda i: (cards[i].normal[axis], i))
        low = count // 2
        cut = min(max(round(len(group) * low / count), low), len(group) - (count - low))
        split(group[:cut], low)
        split(group[cut:], count - low)

    if cards:
        split(list(range(len(cards))), -(-len(cards) // keep))
    return [cards[i] for i in sorted(picked)]


def _recelled(card, cell):
    """The leaf card mapped to `cell`: the same centre, length and facing, as wide as the cell's aspect."""
    c0, c1, _, c3 = card.corners
    centre = scale(tuple(sum(c[a] for c in card.corners) for a in range(3)), 1.0 / len(card.corners))
    a = scale(sub(c3, c0), 0.5)
    (u0, v0), (u1, v1) = CELLS_UV[cell]
    b = scale(normalize(sub(c1, c0)), norm(a) * (u1 - u0) / (v1 - v0))
    corners = [sub(sub(centre, a), b), add(sub(centre, a), b), add(add(centre, a), b), sub(add(centre, a), b)]
    return Card(corners, card.normal, [(u0, v0), (u1, v0), (u1, v1), (u0, v1)], cell)


def add_to(mesh, cards, slot):
    """Adds the cards to a lib.geometry.Mesh as faces of `slot`."""
    for card in cards:
        mesh.face([mesh.vertex(c) for c in card.corners], card.normal, slot, card.uvs, card.normals)


def _off_twig(along, angle, azimuth):
    """The unit direction `angle` radians off the unit twig direction `along`, at `azimuth` about it."""
    e1 = normalize(cross(along, AXES[min(range(3), key=lambda a: abs(along[a]))]))
    e2 = cross(along, e1)
    around = add(scale(e1, math.cos(azimuth)), scale(e2, math.sin(azimuth)))
    return normalize(add(scale(along, math.cos(angle)), scale(around, math.sin(angle))))


def _facing(axis, centre, roll, crown=(0.0, 0.0)):
    """The unit normal of a card along the unit `axis` at `centre`: the clump's outward horizontal direction there
    (about the vertical through `crown`, (x, z)), less its part along the sprig, or up when the sprig points within
    asin(MIN_OUTWARD) of outward, then rolled about the sprig by `roll`."""
    radial = (centre[0] - crown[0], 0.0, centre[2] - crown[1])
    outward = normalize(radial) if norm(radial) > 1e-6 else (1.0, 0.0, 0.0)
    for preferred in (outward, UP):
        n = sub(preferred, scale(axis, dot(preferred, axis)))
        if norm(n) >= MIN_OUTWARD:
            break
    n = normalize(n)
    return add(scale(n, math.cos(roll)), scale(cross(axis, n), math.sin(roll)))
