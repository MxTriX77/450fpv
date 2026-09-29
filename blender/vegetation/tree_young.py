"""A young, half-broken tree of a belt's edge: notes V1's "young or damaged edges 3-8 m".

5.4 m, a thin stem 12 cm across, an open crown with a lot of bare twig in it. These stand where a belt has been
thinned or shelled, and they are what the drone actually brushes when it flies the belt margin low.

    blender -b --factory-startup --python blender/vegetation/tree_young.py   (or tools/blender/export.py --build)

Collision is the trunk cylinder alone: every limb on this tree is under 5 cm, so it bends instead of stopping a
drone and carries `snag_hazard` instead (tools/blender/treekit.py).
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "tools", "blender"))
import assetkit as kit  # noqa: E402
import treekit  # noqa: E402

SEED = 5108

kit.reset()
profile = treekit.Profile(
    height=5.4, trunk_radius=0.06, fork=1.4, crown_radius=1.5, primaries=4, bark="bark_belt",
    stagger=0.75, limb=0.62, taper=0.58, lean=9.0, spread=30.0, split=28.0, children=(4, 3),
    dry=0.22, bare=0.18, density=0.5, card_scale=0.78, porosity=0.62)
lods, materials, entry = treekit.grow(
    profile, SEED, "res://assets/models/vegetation/tree_young.glb", [22.0, 60.0, 150.0], ("#6a6259", 0.85))
kit.finish(__file__, "tree", lods, materials, entry)
