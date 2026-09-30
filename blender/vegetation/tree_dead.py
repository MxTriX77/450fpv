"""A dead, leaning tree with a broken top: notes V2, the tallest thing in open ground and a key silhouette.

7.2 m, a 24 cm grey bark-stripped trunk leaning hard, a bare branch fan about 5 m across and no leaves at all. V2
calls it the hardest thing to see on the feed, so it is also the snag the pilot meets without warning.

    blender -b --factory-startup --python blender/vegetation/tree_dead.py   (or tools/blender/export.py --build)

Collision is the trunk cylinder and the primary limbs as capsules; see tools/blender/treekit.py for the rule.
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "tools", "blender"))
import assetkit as kit  # noqa: E402
import treekit  # noqa: E402

SEED = 7734

kit.reset()
profile = treekit.Profile(
    height=7.2, trunk_radius=0.12, fork=2.6, crown_radius=2.5, primaries=4, bark="bark_dark",
    stagger=0.7, limb=0.66, taper=0.6, lean=13.0, spread=33.0, split=30.0, children=(4, 4),
    leaves=0, density=0.5, porosity=0.8)
lods, materials, entry = treekit.grow(
    profile, SEED, "res://assets/models/vegetation/tree_dead.glb", [30.0, 80.0, 200.0], ("#736c63", 0.9))
kit.finish(__file__, "tree", lods, materials, entry)
