"""White acacia (robinia, біла акація), the tree Soviet-era shelterbelts were planted with: notes V1's belt tree.

Crown 11.6 m to the top, open and irregular, the trunk crooked and forking low, mixed green and yellow foliage with
part-bare patches. Sizes from V1: crowns 6-15 m, trunks 5-30 cm across, shrub understory from about 1 m up; this is
a grown belt tree at the upper middle of that range. Bark is the grey-brown furrowed set (notes V1).

    blender -b --factory-startup --python blender/vegetation/tree_acacia.py   (or tools/blender/export.py --build)

Collision is the trunk cylinder and the primary limbs as capsules. Branches under 4 cm are a deliberate
simplification: they bend and break rather than stop a drone, and they carry `snag_hazard` instead.
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "tools", "blender"))
import assetkit as kit  # noqa: E402
import treekit  # noqa: E402

SEED = 1141

kit.reset()
profile = treekit.Profile(
    height=11.6, trunk_radius=0.13, fork=3.4, crown_radius=2.8, primaries=5, bark="bark_belt",
    stagger=0.5, limb=0.62, taper=0.55, lean=7.0, spread=27.0, split=26.0, children=(5, 4),
    dry=0.18, bare=0.08, porosity=0.55)
lods, materials, entry = treekit.grow(
    profile, SEED, "res://assets/models/vegetation/tree_acacia.glb", [30.0, 80.0, 200.0], ("#6a6259", 0.85))
kit.finish(__file__, "tree", lods, materials, entry)
