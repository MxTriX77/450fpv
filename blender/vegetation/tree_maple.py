"""Box elder (ясенелистий клен), the self-seeded tree that fills every gap in a belt and every abandoned plot.

8.4 m, low and broad, forking close to the ground with a dense rounded crown: the shape that closes a belt's lower
half where the planted trees have been drawn up (notes V1, crowns 6-15 m with young or damaged edges 3-8 m). Its
bark is the dark gnarled set (notes V3).

    blender -b --factory-startup --python blender/vegetation/tree_maple.py   (or tools/blender/export.py --build)

Collision is the trunk cylinder and the primary limbs as capsules; see tools/blender/treekit.py for the rule.
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "tools", "blender"))
import assetkit as kit  # noqa: E402
import treekit  # noqa: E402

SEED = 3312

kit.reset()
profile = treekit.Profile(
    height=8.4, trunk_radius=0.15, fork=2.2, crown_radius=3.1, primaries=4, bark="bark_dark",
    stagger=0.85, limb=0.62, taper=0.6, lean=10.0, spread=37.0, split=30.0, children=(4, 4),
    dry=0.2, bare=0.05, density=1.15, porosity=0.45)
lods, materials, entry = treekit.grow(
    profile, SEED, "res://assets/models/vegetation/tree_maple.glb", [30.0, 80.0, 200.0], ("#5b534a", 0.88))
kit.finish(__file__, "tree", lods, materials, entry)
