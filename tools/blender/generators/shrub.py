"""shrub: a multi-stem understory shrub, V13 of the vegetation sheet (p1-v13-shrub, AST-05, AST-08): `stems` arching
stems from a base disc, twigs branching twice above `bare_base_m`, and a mass of leaf cards over them
(lib.skeleton, lib.cards); twigs leave their stems, and the leaf sprigs their twigs, at the sheet's
`fork_angle_deg`. The origin is the base centre on the ground.

Params (assets-src/vegetation/<variant>.json) are PARAMS below (lib.params checks them). The clump envelope that the
twigs grow out to and the cards fill is an ellipsoid of revolution of the sheet's `width_m`, from `bare_base_m` to
`height_m`, widest at WAIST of that span; for a clump leafy to the ground (`bare_base_m` under GROUND_FADE_M) its
lower half stretches below the ground, which cuts it, so the leaves reach the ground (the sheet's low variant).
Stems run STEM_SHARE of the height, inside the crown, from a base disc of BASE_R_SHARE * width_m within BASE_R_M.
The card count is cards_per_m3 * the envelope's volume * leaf_cover.

LODs: LOD0 6-sided stems, 3-sided twigs and every card; LOD1 4-sided stems and every other card; LOD2 three crossed
cards spanning the crown LOD0 built (its twigs and cards). lod.* come from the pixel rule (935 px/rad, setup-asset-pipeline design decision 7):
LOD1 where the thickest twig, 8 mm, drops under 1 px (7.5 m); LOD2 where a card drops under 2 px (card_m * 467);
cull where the clump's height drops under 2 px (height_m * 467, capped at 800 m: the belt beyond is V01's band).

Metadata (design decisions 4 and 7, review-physics PR1): a `bark` capsule per stem segment (3 per stem) at the
segment's mean radius; a `hook` snag per stem from base to tip, radius half the base diameter; the crown contact
shell as `twig` spheres and capsules on the leaf and twig mass (lib.shell, at most 24, which the audit holds both
ways); one wind box over the clump at porosity 0.7 - 0.35 * leaf_cover (the sheet's 0.35 summer, 0.5 autumn, 0.7
bare); no gaps; nothing landable.

Slots: stem (bark_shrub, physics bark), twig (bark_shrub, physics twig: the twigs are the shell's contact, not the
capsules'), leaf (leaf_shrub, physics twig).
"""
import math
import random

from lib import cards, meta, shell, skeleton
from lib.geometry import Mesh

STEM, TWIG, LEAF = "stem", "twig", "leaf"
CARD_BUDGET_TRIS = 12000
WAIST = 0.4
GROUND_FADE_M = 0.3
GROUND_STRETCH = 0.75
STEM_SHARE = (0.75, 0.95)
BASE_R_SHARE, BASE_R_M = 0.1, (0.1, 0.25)
CARD_MARGIN = 0.8           # of card_m: a sprig at a twig end reaches up to its length beyond it; 0.8 keeps
                            # the built width within 5 % of width_m (measured on the three variants)
MIN_AXIS_M = 0.05
IDENTITY = [0.0, 0.0, 0.0, 1.0]
# Params the sheet gives the generator that nothing can draw yet: a non-default value is refused rather than
# silently ignored (the proposal: the parameters exist, G1 shows summer).
UNBUILT = (("autumn", 0.0, "a per-variant leaf tint needs the importer to read one; the look-lock decides"),
           ("spines", False, "no spine cell is pinned"),
           ("fruit_tufts", 0, "no berry cell is pinned"))
# The object sheet's shared shrub table and §V13; defaults are V13_mid.
PARAMS = {
    "seed": {"type": "int", "range": (0, 2**31 - 1), "default": 1, "effect": "all draws: stem places, twigs, cards"},
    "height_m": {"type": "float", "range": (0.3, 4.0), "default": 2.0, "effect": "clump height; the cull distance follows it"},
    "width_m": {"type": "float", "range": (0.3, 4.0), "default": 2.2, "effect": "clump footprint and the LOD2 cards' width"},
    "stems": {"type": "int", "range": (1, 40), "default": 14, "effect": "stems from the base disc, a capsule chain and a hook each"},
    "stem_d_mm": {"type": "float", "range": (10.0, 60.0), "default": 18.0,
                  "effect": "base diameter of every stem, tapering to 10 mm at the tip; thinner wood is a twig"},
    "arch": {"type": "float", "range": (0.0, 1.0), "default": 0.15, "effect": "the tip's outward drift as a share of the stem's height"},
    "fork_angle_deg.min": {"type": "float", "range": (10.0, 90.0), "default": 40.0,
                           "effect": "least angle at which a twig leaves its stem and a leaf sprig its twig"},
    "fork_angle_deg.max": {"type": "float", "range": (10.0, 90.0), "default": 70.0,
                           "effect": "greatest such angle; wider fills the clump's sides and underside"},
    "bare_base_m": {"type": "float", "range": (0.0, 1.5), "default": 0.6,
                    "effect": "no twigs or cards below this height; under 0.3 the leaves reach the ground"},
    "leaf_cover": {"type": "float", "range": (0.0, 1.0), "default": 1.0, "effect": "scales the card count; porosity 0.7 - 0.35 * leaf_cover"},
    "autumn": {"type": "float", "range": (0.0, 1.0), "default": 0.0, "effect": "lerps the leaf tint to the sheet's yellow (not built: see UNBUILT)"},
    "spines": {"type": "bool", "range": "true or false", "default": False, "effect": "spine cards on the stems, the silverberry look (not built)"},
    "fruit_tufts": {"type": "int", "range": (0, 60), "default": 0, "effect": "1-card berry clusters (not built)"},
    "card_m": {"type": "float", "range": (0.1, 0.5), "default": 0.2, "effect": "a leaf sprig card's length from its stalk on the twig; its width follows the cell"},
    "cards_per_m3": {"type": "float", "range": (1.0, 5000.0), "default": 900.0,
                     "effect": "cards per m3 of the clump envelope at leaf_cover 1; under 6000 cards (12 000 triangles)"},
    "lod.lod1_switch_m": {"type": "float", "range": (0.1, 100.0), "default": 7.5,
                          "effect": "distance from which LOD1 shows: the 8 mm twigs under 1 px"},
    "lod.lod2_switch_m": {"type": "float", "range": (1.0, 500.0), "default": 93.5,
                          "effect": "distance from which LOD2 shows: a card under 2 px, card_m * 467"},
    "lod.cull_m": {"type": "float", "range": (1.0, 2000.0), "default": 800.0,
                   "effect": "distance beyond which the clump is culled: its height under 2 px, height_m * 467, at most 800"},
}


class Envelope:
    """The clump's leafy volume: an ellipsoid of revolution about the clump axis, radius `rw`, waist at `yc` with
    the semi-axes `lo` below and `hi` above, shrunk by `margin` so a card at a twig end stays inside the clump."""

    def __init__(self, width, y0, y1, margin=0.0):
        self.rw = max(width / 2 - margin, MIN_AXIS_M)
        self.yc = y0 + WAIST * (y1 - y0)
        self.hi = max(y1 - margin - self.yc, MIN_AXIS_M)
        self.lo = max(self.yc - y0 - margin, MIN_AXIS_M) * (1 + GROUND_STRETCH * max(0.0, 1 - y0 / GROUND_FADE_M))

    def inside(self, p):
        a = self.lo if p[1] < self.yc else self.hi
        return (math.hypot(p[0], p[2]) / self.rw) ** 2 + ((p[1] - self.yc) / a) ** 2 <= 1

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

    def volume(self):
        return 2 / 3 * math.pi * self.rw ** 2 * (self.lo + self.hi)


def card_count(params):
    return round(params["cards_per_m3"] * Envelope(params["width_m"], params["bare_base_m"], params["height_m"]).volume()
                 * params["leaf_cover"])


def check(params):
    """Cross-parameter rules once every key is in range: the fork angles are ordered, a leafy zone exists, the cards
    fit the LOD0 budget, and nothing asks for a look that is not built."""
    problems = []
    fork = params["fork_angle_deg"]
    if fork["min"] > fork["max"]:
        problems.append(f"'fork_angle_deg.min' = {fork['min']} must not exceed 'fork_angle_deg.max' = {fork['max']}")
    if params["bare_base_m"] >= params["height_m"]:
        problems.append(f"'bare_base_m' = {params['bare_base_m']} must stay under 'height_m' = {params['height_m']}")
    else:
        count = card_count(params)
        if 2 * count > CARD_BUDGET_TRIS:
            problems.append(f"'cards_per_m3' = {params['cards_per_m3']} gives {count} cards, {2 * count} triangles at LOD0, "
                            f"over the budget of {CARD_BUDGET_TRIS}")
    for key, idle, why in UNBUILT:
        if params[key] != idle:
            problems.append(f"'{key}' = {params[key]!r} is not built yet ({why}); keep {idle!r}")
    return problems


def build(params):
    rng = random.Random(params["seed"])
    height, width, bare = params["height_m"], params["width_m"], params["bare_base_m"]
    card_m, stem_r = params["card_m"], params["stem_d_mm"] / 2000
    envelope = Envelope(width, bare, height, CARD_MARGIN * card_m)
    base_r = min(max(BASE_R_SHARE * width, BASE_R_M[0]), BASE_R_M[1])
    fork = (params["fork_angle_deg"]["min"], params["fork_angle_deg"]["max"])
    stems, twigs = skeleton.build(rng, params["stems"], base_r, (STEM_SHARE[0] * height, STEM_SHARE[1] * height),
                                  stem_r, params["arch"], bare, envelope.reach, fork)
    leaves = cards.place(rng, twigs, card_count(params), card_m, fork_deg=fork)
    lods = [Mesh(), Mesh(), Mesh()]
    for lod, mesh in enumerate(lods[:2]):
        skeleton.tubes(mesh, stems, twigs, lod, STEM, TWIG)
        cards.add_to(mesh, leaves[::lod + 1], LEAF)
    crown = sorted({lods[0].vertices[i] for indices, _, _, slot in lods[0].faces if slot in (TWIG, LEAF) for i in indices})
    collision = [meta.capsule(p0, p1, (r0 + r1) / 2, "bark") for stem in stems for p0, p1, r0, r1 in stem.segments]
    if crown:  # the far cards span the built crown, not the envelope, so LOD2 shows the clump LOD0 showed
        clo, chi = (tuple(f(p[a] for p in crown) for a in range(3)) for f in (min, max))
        cards.add_to(lods[2], cards.crossed(max(chi[0] - clo[0], chi[2] - clo[2]), clo[1], chi[1]), LEAF)
        collision += [meta.capsule(a, b, r, "twig") for a, b, r in shell.fit(lods[0], (TWIG,), (LEAF,))]
    else:
        lods.pop()
    lo, hi = lods[0].bounds()
    lod = params["lod"]
    return {
        "lods": lods,
        "switch_distances_m": [0.0, lod["lod1_switch_m"], lod["lod2_switch_m"]][:len(lods)],
        "cull_distance_m": lod["cull_m"],
        "materials": [{"slot": STEM, "physics": "bark"}, {"slot": TWIG, "physics": "twig"}, {"slot": LEAF, "physics": "twig"}],
        "collision": collision,
        "gaps": [],
        "wind_volumes": [{"shape": "box", "center": [(lo[a] + hi[a]) / 2 for a in range(3)], "size": [hi[a] - lo[a] for a in range(3)],
                          "rotation": IDENTITY, "porosity": 0.7 - 0.35 * params["leaf_cover"]}],
        "snag": [{"kind": "hook", "points": [list(s[0]) for s in stem.segments] + [list(stem.segments[-1][1])], "radius_m": stem_r}
                 for stem in stems],
    }

