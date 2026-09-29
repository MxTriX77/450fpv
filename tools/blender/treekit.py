"""Grows the tree-belt vegetation of reference notes V1, V2 and V3 for the builders in blender/vegetation/.

One profile describes a tree: its height, how its trunk forks, how its limbs split, and how much of its crown is
green, dry or already bare. `grow` turns that into the four LOD meshes and the catalog entry:

- **LOD0-2 are drawn, not decimated.** A crown is loose alpha cards, and a decimate modifier collapses them into
  torn rubbish, so each level is grown again with fewer, larger cards and fewer branch sides. Leaf area is kept
  roughly constant between levels (cards x size^2), so the crown neither thins nor swells at a switch.
- **LOD3 is the far impostor**: two crossed cards of the atlas's whole-tree tile plus one flat card, so the crown
  still has a top when the drone looks down on the belt from above.
- **Leaves sit at the branch tips**, where they grow, and each card's shading normal points away from the crown
  centre, so a crown is lit as one rounded mass and not as a heap of separate plates.

Collision is the trunk as a cylinder and each primary limb as a capsule: what a drone hits and stops against. Every
branch below `SOLID_RADIUS` is left out as a deliberate simplification (asset-pipeline, collision follows the visual):
a 3 cm twig bends or breaks, and the sim treats it through `snag_hazard`, not as a wall.
"""
import math
import random

import assetkit as kit

SOLID_RADIUS = 0.05     # m: a limb thinner than this gets no collision shape, only the snag flag
GREEN, DRY, TWIGS, IMPOSTOR = (0, 0), (1, 0), (0, 1), (1, 1)  # foliage atlas tiles, from the image's top-left


class Profile:
    """A tree's measurements, all in metres and degrees, from the reference notes."""

    def __init__(self, height, trunk_radius, fork, crown_radius, primaries, bark, **rest):
        self.height = height                # ground to the highest twig: the top of the crown envelope
        self.trunk_radius = trunk_radius    # at the ground
        self.fork = fork                    # where the lowest primary limb leaves the trunk, and the crown's underside
        self.crown_radius = crown_radius    # half the crown's width (notes V1: trunks 1-3 m apart, so crowns are narrow)
        self.primaries = primaries          # number of primary limbs
        self.bark = bark                    # material name
        self.stagger = rest.get("stagger", 0.55)      # how far above `fork` the last primary leaves, as a fraction of it
        self.limb = rest.get("limb", 0.62)            # primary limb length over the crown's height
        self.taper = rest.get("taper", 0.56)          # each split's length over its parent's
        self.lean = rest.get("lean", 6.0)             # degrees the trunk wanders off vertical
        self.spread = rest.get("spread", 42.0)        # degrees a primary limb rises away from the trunk
        self.split = rest.get("split", 34.0)          # degrees each further split turns
        self.children = rest.get("children", (4, 3))  # splits per limb at depth 1 and 2
        self.leafy_depth = rest.get("leafy_depth", 2)  # branches from this depth on carry leaves along their length
        self.leaves = rest.get("leaves", 1.0)         # 0 leaves the tree bare (notes V2)
        self.dry = rest.get("dry", 0.22)              # fraction of leaf cards in the dry, yellow tile
        self.bare = rest.get("bare", 0.1)             # fraction that are bare twigs: part-bare crowns (notes V1)
        self.porosity = rest.get("porosity", 0.55)    # open fraction of the crown seen side-on
        self.density = rest.get("density", 1.0)       # cards against LEVELS: below 1 for an open crown
        self.card_scale = rest.get("card_scale", 1.0)  # card size against LEVELS: below 1 for a small tree
        self.foliage = rest.get("foliage", "foliage_belt")
        self.impostor = rest.get("impostor", IMPOSTOR)  # GREEN for a bush, which has no trunk to draw
        self.rise = (self.height + self.fork) / 2      # the crown envelope: an ellipsoid from the fork to the top
        self.half = (self.height - self.fork) / 2

    def outside(self, point):
        """How far the point is out of the crown envelope, 1 on its surface. Growth stops there, so the tree keeps
        the height and width the notes give it instead of throwing a shoot twice as high as its neighbours."""
        return (point[0] / self.crown_radius) ** 2 + ((point[1] - self.rise) / self.half) ** 2 \
            + (point[2] / self.crown_radius) ** 2

    def clip(self, start, end):
        """The fraction of the step start -> end that stays inside the envelope, found by bisection."""
        if self.outside(end) <= 1:
            return 1.0
        low, high = 0.0, 1.0
        for _ in range(24):
            middle = (low + high) / 2
            if self.outside(kit._add(start, kit._scale(kit._sub(end, start), middle))) <= 1:
                low = middle
            else:
                high = middle
        return low


# Per LOD level: how deep the branches are drawn, the sides of a trunk/limb/twig tube, and the crown's cards and their
# size in metres at `density` 1. cards x card_m^2 is the same at every level (344 m^2 of leaf), so the crown keeps its
# depth and colour through a switch and only its grain coarsens.
LEVELS = [
    {"depth": 3, "sides": (10, 8, 6, 5), "cards": 820, "card_m": 0.80},
    {"depth": 2, "sides": (8, 6, 5, 4), "cards": 285, "card_m": 1.36},
    {"depth": 1, "sides": (6, 5, 4, 4), "cards": 98, "card_m": 2.32},
]


def _turn(direction, angle, azimuth):
    """`direction` turned `angle` radians away from itself, around the axis picked by `azimuth`."""
    x, y = kit._frame(direction)
    side = kit._add(kit._scale(x, math.cos(azimuth)), kit._scale(y, math.sin(azimuth)))
    return kit._normalize(kit._add(kit._scale(direction, math.cos(angle)), kit._scale(side, math.sin(angle))))


def _limb(mesh, rng, profile, level, start, direction, length, radius, depth, leafy, shapes):
    """Draws one limb and its children, collects the points foliage hangs on and the shapes the limb collides as."""
    bends, points, radii = 3, [start], [radius]
    here, heading, stopped = start, direction, False
    for step in range(bends):
        # Limbs wander and lift: a belt tree's branches are crooked, never straight rods (notes V1).
        heading = _turn(heading, math.radians(rng.uniform(4, 13)), rng.uniform(0, 2 * math.pi))
        heading = kit._normalize(kit._add(heading, (0, 0.06 if depth else 0.02, 0)))
        reached = profile.clip(here, kit._add(here, kit._scale(heading, length / bends)))
        here = kit._add(here, kit._scale(heading, length / bends * reached))
        points.append(here)
        stopped = reached < 1 or step == bends - 1
        radii.append(0.0 if stopped and depth >= level["depth"] else radius * (1 - 0.72 * (step + 1) / bends))
        if reached < 1:
            break
    if len(points) < 2 or math.dist(points[0], points[-1]) < 1e-3:
        return  # the limb started on the envelope and had nowhere to grow
    mesh.tube(points, radii, level["sides"][min(depth, 3)], profile.bark)
    if radius >= SOLID_RADIUS and shapes is not None:
        shapes.append(_capsule(start, here, radius))
    if depth >= profile.leafy_depth:
        # Leaves grow all along the outer branches, not only at their ends, so the crown has no hollow inside.
        leafy += points[1:]
    if depth >= level["depth"]:
        if depth < profile.leafy_depth:
            leafy.append(here)
        return
    count = profile.children[min(depth, len(profile.children) - 1)]
    drawn = sum(math.dist(a, b) for a, b in zip(points, points[1:]))
    for i in range(count):
        base, walked = points[-1], (0.45 + 0.55 * (i + 0.5) / count) * drawn
        for a, b in zip(points, points[1:]):  # walk that far along the limb to find where the child leaves it
            span = math.dist(a, b)
            if walked <= span:
                base = kit._add(a, kit._scale(kit._normalize(kit._sub(b, a)), walked))
                break
            walked -= span
        child = _turn(heading, math.radians(profile.split * rng.uniform(0.7, 1.3)),
                      2 * math.pi * (i + rng.uniform(0, 0.6)) / count)
        _limb(mesh, rng, profile, level, base, child, drawn * profile.taper * rng.uniform(0.85, 1.15),
              radius * 0.52, depth + 1, leafy, shapes)


def _capsule(start, end, radius):
    """A capsule along the limb, in the catalog's asset space and rotation order."""
    direction = kit._normalize(kit._sub(end, start))
    length = math.dist(start, end)
    centre = kit._scale(kit._add(start, end), 0.5)
    pitch = math.degrees(math.acos(max(-1.0, min(1.0, direction[1]))))
    yaw = math.degrees(math.atan2(direction[0], direction[2]))
    shape = {"shape": "capsule", "radius_m": round(radius, 4), "height_m": round(length + 2 * radius, 4),
             "position_m": [round(c, 4) for c in centre]}
    if round(pitch, 3):
        shape["rotation_deg"] = [round(yaw, 3), round(pitch, 3), 0.0]
    return shape


def _crown(mesh, rng, profile, level, leafy):
    """Leaf, dry and bare-twig cards spread along the outer branches, each facing away from the crown's centre."""
    centre = (0.0, profile.rise, 0.0)
    half = level["card_m"] * profile.card_scale / 2
    spread = level["card_m"] * profile.card_scale * 0.42
    for i in range(round(level["cards"] * profile.density)):
        spot = kit._add(leafy[(i * 7919) % len(leafy)], tuple(rng.uniform(-spread, spread) for _ in range(3)))
        out = kit._normalize(kit._sub(spot, centre))
        # The cards face every way, so from any heading a good share of them is seen flat and the rest edge-on;
        # their shading normal is the outward one, which is what makes the crown read as one rounded mass.
        plane = kit._normalize(tuple(rng.gauss(0, 1) for _ in range(3)))
        across, up = kit._frame(plane)
        spin = rng.uniform(0, 2 * math.pi)
        a = kit._add(kit._scale(across, math.cos(spin) * half), kit._scale(up, math.sin(spin) * half))
        b = kit._add(kit._scale(across, -math.sin(spin) * half), kit._scale(up, math.cos(spin) * half))
        roll = rng.random()
        tile = TWIGS if profile.leaves == 0 or roll < profile.bare \
            else DRY if roll < profile.bare + profile.dry else GREEN
        mesh.card(spot, a, b, profile.foliage, tile, normal=out)


def _impostor(profile):
    """The last level: two crossed cards of the whole tree, plus one lying flat across the crown so the belt still
    has a canopy when the drone looks down on it."""
    mesh = kit.Mesh()
    wide = profile.crown_radius * 1.35  # the atlas tile's crown fills about 0.74 of its width
    high = profile.height / 2
    for angle in (0.0, math.pi / 2):
        across = (math.cos(angle) * wide, 0.0, math.sin(angle) * wide)
        mesh.card((0.0, high, 0.0), across, (0.0, high, 0.0), profile.foliage, profile.impostor,
                  normal=(-math.sin(angle), 0.25, math.cos(angle)))
    flat = profile.crown_radius
    mesh.card((0.0, profile.rise, 0.0), (flat, 0.0, 0.0), (0.0, 0.0, flat), profile.foliage, GREEN,
              normal=(0.0, 1.0, 0.0))
    return mesh


def _skeleton(mesh, rng, profile, level, shapes):
    """The trunk and every limb of one level. Returns the points foliage hangs on."""
    leafy = []
    trunk, radius, heading = [(0.0, 0.0, 0.0)], [profile.trunk_radius], (0.0, 1.0, 0.0)
    here = trunk[0]
    # Two segments of bare trunk up to the crown's underside, then one per primary: the limbs leave the trunk one
    # above the other, so the tree has a trunk and not a Y of equal stems.
    rises = [profile.fork / 2] * 2 + [profile.fork * profile.stagger / profile.primaries] * profile.primaries
    for step, rise in enumerate(rises):
        heading = _turn(heading, math.radians(rng.uniform(0.3, 1.0) * profile.lean), rng.uniform(0, 2 * math.pi))
        here = kit._add(here, kit._scale(heading, rise))
        trunk.append(here)
        radius.append(profile.trunk_radius * (1 - 0.55 * (step + 1) / len(rises)))
    mesh.tube(trunk, radius, level["sides"][0], profile.bark)
    if shapes is not None:
        shapes.append({"shape": "cylinder", "radius_m": round(profile.trunk_radius, 4),
                       "position_m": [0.0, round(profile.fork / 2, 4), 0.0], "height_m": round(profile.fork, 4)})
    reach = (profile.height - profile.fork) * profile.limb
    for i in range(profile.primaries):
        at = 3 + i
        direction = _turn(heading, math.radians(profile.spread * rng.uniform(0.75, 1.25)),
                          2 * math.pi * (i + rng.uniform(0, 0.5)) / profile.primaries)
        _limb(mesh, rng, profile, level, trunk[at], direction, reach * rng.uniform(0.82, 1.18),
              radius[at] * 0.78, 1, leafy, shapes)
    return leafy


def grow(profile, seed, scene, lod_switch_m, bark_colour):
    """The four LOD meshes and the catalog entry of one tree, ready for kit.finish."""
    lods, shapes = [], []
    for n, level in enumerate(LEVELS):
        rng = random.Random(seed)  # every level grows the same tree, so the silhouette does not move at a switch
        mesh = kit.Mesh()
        leafy = _skeleton(mesh, rng, profile, level, shapes if n == 0 else None)
        _crown(mesh, rng, profile, level, leafy)
        lods.append(mesh)
    lods.append(_impostor(profile))

    entry = {
        "type": "object",
        "scene": scene,
        "material": "timber",
        "collision": shapes,
        # The crown envelope itself: a cylinder around it, so the wind grid gets the right footprint, top and base.
        "wind_volume": [{"shape": "cylinder", "radius_m": round(profile.crown_radius, 4),
                         "height_m": round(profile.height - profile.fork, 4),
                         "position_m": [0.0, round(profile.rise, 4), 0.0]}],
        "snag_hazard": True,
        "wind_porosity": profile.porosity,
        "gaps": [],
        "lod_switch_m": lod_switch_m,
    }
    # The .glb carries flat colours only; game/assets/materials/<name>.tres holds the real textures.
    materials = {profile.bark: bark_colour, profile.foliage: ("#5c6a38", 0.82, "cutout")}
    return lods, materials, entry
