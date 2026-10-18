"""Leaf cards for the vegetation generators (p1-v13-shrub, design decisions 2 and 3): double-sided quads along the
twigs, each mapped into one sprig cell of the LeafSet002 atlas, and the far LOD's crossed cards. Plain Python in
asset space, drawing from the caller's seeded random.Random.

A card is `card_m` long along its twig (the sprig grows along the twig, stalk toward the twig's base) and as wide
as its cell's aspect, centred on the twig at a parameter biased toward the tip (t = 1 - u^2, the leaves sit
outside) and rolled about the twig at random; second-order twigs draw LEVEL_WEIGHT times as many cards as
first-order ones. The leaf recipe is two-sided and alpha-clipped, so one quad, two triangles, shows both faces. A
card whose corners would dip under the ground is lifted onto it. The far LOD is CROSSED vertical cards of the whole
clump mapped to the whole atlas (the atlas scaled up).

The cells are LeafSet002's six sprigs, measured on its 2048 px map (design 3, "as built") as inclusive pixel
columns and rows, row 0 at the top, so V runs up and the stalk sits at the cell's V0; the recipe tiles the atlas at
1.0 m, so UV metres are atlas fractions.
"""
import math

from .skeleton import add, cross, normalize, scale, sub

SET_PX = 2048
CELLS_PX = (((192, 128), (560, 960)), ((784, 128), (1264, 960)), ((1472, 128), (1856, 960)),
            ((192, 1152), (560, 1952)), ((784, 1152), (1264, 1952)), ((1472, 1152), (1856, 1952)))
CROSSED = 3
LEVEL_WEIGHT = {1: 1, 2: 3}
MIN_T = 0.15
AXES = ((1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0))


def cell_uv(cell_px, set_px=SET_PX):
    """A pixel box of the atlas as (min, max) UV corners: row 0 is the top of the map, V runs up."""
    (x0, y0), (x1, y1) = cell_px
    return ((x0 / set_px, 1 - (y1 + 1) / set_px), ((x1 + 1) / set_px, 1 - y0 / set_px))


CELLS_UV = tuple(cell_uv(c) for c in CELLS_PX)


class Card:
    """A quad: `corners` in cyclic order (stalk end first), one face `normal`, the `uvs` per corner, its `cell`."""

    def __init__(self, corners, normal, uvs, cell):
        self.corners, self.normal, self.uvs, self.cell = corners, normal, uvs, cell


def place(rng, twigs, count, card_m, ground_y=0.0):
    """`count` cards over the `twigs` (lib.skeleton.Twig), each `card_m` long."""
    if not twigs or count <= 0:
        return []
    weights = [LEVEL_WEIGHT.get(twig.level, 1) for twig in twigs]
    cards = []
    for index in rng.choices(range(len(twigs)), weights=weights, k=count):
        twig = twigs[index]
        u = rng.random()
        t = max(MIN_T, 1 - u * u)
        axis = normalize(sub(twig.p1, twig.p0))
        centre = add(twig.p0, scale(sub(twig.p1, twig.p0), t))
        side = _perpendicular(axis, rng.uniform(0, 2 * math.pi))
        cell = rng.randrange(len(CELLS_UV))
        (u0, v0), (u1, v1) = CELLS_UV[cell]
        half_l = card_m / 2
        half_w = half_l * (u1 - u0) / (v1 - v0)
        a, b = scale(axis, half_l), scale(side, half_w)
        corners = [sub(sub(centre, a), b), add(sub(centre, a), b), add(add(centre, a), b), sub(add(centre, a), b)]
        lift = ground_y - min(c[1] for c in corners)
        if lift > 0:
            corners = [(x, y + lift, z) for x, y, z in corners]
        cards.append(Card(corners, cross(axis, side), [(u0, v0), (u1, v0), (u1, v1), (u0, v1)], cell))
    return cards


def crossed(width, y0, y1, count=CROSSED):
    """The far LOD: `count` vertical cards of the clump, `width` wide from y0 to y1, crossed about the axis at even
    azimuths, each mapped to the whole atlas."""
    cards = []
    for k in range(count):
        azimuth = math.pi * k / count
        b = scale((math.cos(azimuth), 0.0, math.sin(azimuth)), width / 2)
        corners = [(-b[0], y0, -b[2]), (b[0], y0, b[2]), (b[0], y1, b[2]), (-b[0], y1, -b[2])]
        cards.append(Card(corners, cross((0.0, 1.0, 0.0), normalize(b)), [(0.0, 0.0), (1.0, 0.0), (1.0, 1.0), (0.0, 1.0)], None))
    return cards


def add_to(mesh, cards, slot):
    """Adds the cards to a lib.geometry.Mesh as faces of `slot`."""
    for card in cards:
        mesh.face([mesh.vertex(c) for c in card.corners], card.normal, slot, card.uvs)


def _perpendicular(axis, roll):
    helper = AXES[min(range(3), key=lambda a: abs(axis[a]))]
    u = normalize(cross(axis, helper))
    v = cross(axis, u)
    return add(scale(u, math.cos(roll)), scale(v, math.sin(roll)))
