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

The far LOD is CROSSED vertical cards of the whole clump, each mapped to the dense core of one sprig, FAR_PX, the
atlas scaled up: the whole atlas is 20 % leaf, so at the 9 px of a far clump its six thin sprigs fell under the
clip and the card vanished (measured on the first review sheet at 200 m).

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
# The densest box of 0.6 x 0.5 of a cell in cells 3, 5 and 2: 95, 92 and 91 % of its texels opaque (alpha >= 0.5,
# measured on leaf_shrub_base.png), against 30-52 % for a whole cell and 20 % for the atlas.
FAR_PX = (((288, 1312), (495, 1711)), ((1568, 1296), (1791, 1695)), ((1568, 352), (1791, 767)))
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


class Card:
    """A quad: `corners` in cyclic order (stalk end first), one face `normal`, the `uvs` per corner, its `cell`."""

    def __init__(self, corners, normal, uvs, cell):
        self.corners, self.normal, self.uvs, self.cell = corners, normal, uvs, cell


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


def crossed(width, y0, y1, count=CROSSED):
    """The far LOD: `count` vertical cards of the clump, `width` wide from y0 to y1, crossed about the axis at even
    azimuths, card k mapped to the sprig core FAR_PX[k]."""
    cards = []
    for k in range(count):
        azimuth = math.pi * k / count
        b = scale((math.cos(azimuth), 0.0, math.sin(azimuth)), width / 2)
        corners = [(-b[0], y0, -b[2]), (b[0], y0, b[2]), (b[0], y1, b[2]), (-b[0], y1, -b[2])]
        (u0, v0), (u1, v1) = cell_uv(FAR_PX[k % len(FAR_PX)])
        cards.append(Card(corners, cross(UP, normalize(b)), [(u0, v0), (u1, v0), (u1, v1), (u0, v1)], None))
    return cards


def add_to(mesh, cards, slot):
    """Adds the cards to a lib.geometry.Mesh as faces of `slot`."""
    for card in cards:
        mesh.face([mesh.vertex(c) for c in card.corners], card.normal, slot, card.uvs)


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
