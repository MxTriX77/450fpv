"""Ash (ясен), the other tree Ukrainian shelterbelts were planted with: the tall, straight one of notes V1.

13.5 m to the top, near the upper end of V1's 6-15 m crowns, with a straight trunk, steeply rising limbs and a
narrow crown. It is the tree that gives a belt its high, even skyline when seen from the field.

    blender -b --factory-startup --python blender/vegetation/tree_ash.py   (or tools/blender/export.py --build)

Collision is the trunk cylinder and the primary limbs as capsules; see tools/blender/treekit.py for the rule.
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "tools", "blender"))
import assetkit as kit  # noqa: E402
import treekit  # noqa: E402

SEED = 2207

kit.reset()
profile = treekit.Profile(
    height=13.5, trunk_radius=0.16, fork=5.0, crown_radius=2.6, primaries=5, bark="bark_belt",
    stagger=0.45, limb=0.62, taper=0.56, lean=4.0, spread=21.0, split=22.0, children=(4, 4),
    dry=0.14, bare=0.06, porosity=0.5)
lods, materials, entry = treekit.grow(
    profile, SEED, "res://assets/models/vegetation/tree_ash.glb", [30.0, 80.0, 200.0], ("#6f675c", 0.85))
kit.finish(__file__, "tree", lods, materials, entry)
