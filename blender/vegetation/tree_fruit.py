"""Yard fruit tree, notes V3: the apple or cherry standing in the yard in clip C, one or two per village yard.

Sizes from V3: 4-7 m tall, trunk 20-40 cm across, a low spreading crown 5-8 m wide with branches from about 1.5 m,
dark gnarled bark, sparse autumn leaves. An old unpruned yard tree: it forks low into four heavy limbs that reach out
almost as far as the tree is tall, so the crown is wider than it is high and the branches hang right where a yard
approach is flown (V3 `obstacle` and `snag`).

    blender -b --factory-startup --python blender/vegetation/tree_fruit.py   (or tools/blender/export.py --build)

Collision is the trunk cylinder and the four primary limbs as capsules, as for the belt trees; branches under 5 cm
bend or break instead of stopping a drone and are carried by `snag_hazard` (treekit).
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "tools", "blender"))
import assetkit as kit  # noqa: E402
import treekit  # noqa: E402

SEED = 6158

kit.reset()
profile = treekit.Profile(
    height=6.0, trunk_radius=0.16, fork=1.5, crown_radius=3.2, primaries=4, bark="bark_dark",
    stagger=0.4, limb=0.82, taper=0.64, lean=11.0, spread=54.0, split=40.0, children=(4, 3),
    dry=0.5, bare=0.22, density=0.6, card_scale=0.85, porosity=0.62)
lods, materials, entry = treekit.grow(
    profile, SEED, "res://assets/models/vegetation/tree_fruit.glb", [26.0, 70.0, 170.0], ("#4f473d", 0.9))
kit.finish(__file__, "tree", lods, materials, entry)
